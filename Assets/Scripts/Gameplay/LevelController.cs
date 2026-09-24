using System;
using System.Collections;
using System.Collections.Generic;
using ColorMelt.Core;
using ColorMelt.Meta;
using UnityEngine;

namespace ColorMelt.Gameplay
{
    public readonly struct LevelResult
    {
        public readonly int stars;
        public readonly int movesUsed;
        public readonly int reward;

        public LevelResult(int stars, int movesUsed, int reward)
        {
            this.stars = stars;
            this.movesUsed = movesUsed;
            this.reward = reward;
        }
    }

    /// <summary>
    /// Runs one level: builds it, spends moves on pours, plays melts with a
    /// short delay so the paint visibly reaches the block, and reports win or
    /// lose. Game rules live in FlowModel; this class only sequences them.
    /// </summary>
    public class LevelController : MonoBehaviour
    {
        [SerializeField] private LevelBuilder builder;
        [SerializeField] private Camera gameCamera;

        [Tooltip("Editor only: play this level instead of the one chosen in the menu.")]
        [SerializeField] private LevelData testLevel;

        [Header("Timing")]
        [Tooltip("Time for poured paint to run down to a block before it melts.")]
        [SerializeField, Min(0f)] private float flowDelay = 0.45f;
        [SerializeField, Min(0f)] private float meltTime = 0.4f;

        [Header("Camera")]
        [Tooltip("Route count the camera pose in the scene was framed for.")]
        [SerializeField, Min(1)] private int framedRouteCount = 3;

        private readonly List<RouteView> _routes = new List<RouteView>();
        private ColorType[] _lastReached;
        private Vector3 _cameraRestPosition;
        private bool _resolving;
        private bool _finished;

        public event Action Started;
        public event Action<int> MovesChanged;
        public event Action<Move> Poured;
        public event Action<RouteView, BlockView> BlockMelted;
        public event Action<LevelResult> Won;
        public event Action Lost;
        public event Action Continued;

        public LevelData Level { get; private set; }
        public int LevelIndex { get; private set; }
        public FlowModel Model { get; private set; }
        public IReadOnlyList<RouteView> Routes => _routes;
        public Camera Camera => gameCamera;
        public int MovesLeft { get; private set; }
        public int MovesUsed { get; private set; }

        /// <summary>True while melts are playing or the level is over.</summary>
        public bool IsBusy => _resolving || _finished;

        private void Awake()
        {
            if (gameCamera == null)
                gameCamera = Camera.main != null ? Camera.main : FindAnyObjectByType<Camera>();
            if (gameCamera != null)
                _cameraRestPosition = gameCamera.transform.position;
        }

        private void Start()
        {
            LevelIndex = GameSession.LevelIndex;
            Level = LevelDatabase.Instance != null ? LevelDatabase.Instance.Get(LevelIndex) : null;
#if UNITY_EDITOR
            // "Play This Level" in a LevelData inspector leaves its path here.
            var editorLevel = testLevel;
            var requested = UnityEditor.EditorPrefs.GetString("ColorMelt.TestLevel", "");
            if (!string.IsNullOrEmpty(requested))
            {
                UnityEditor.EditorPrefs.DeleteKey("ColorMelt.TestLevel");
                var requestedLevel = UnityEditor.AssetDatabase.LoadAssetAtPath<LevelData>(requested);
                if (requestedLevel != null) editorLevel = requestedLevel;
            }
            if (editorLevel != null)
            {
                Level = editorLevel;
                var index = LevelDatabase.Instance != null ? LevelDatabase.Instance.IndexOf(editorLevel) : -1;
                LevelIndex = index >= 0 ? index : LevelIndex;
                GameSession.SetCurrent(LevelIndex);
            }
#endif
            if (Level == null)
            {
                Debug.LogError("LevelController: no level to play. Fill Resources/LevelDatabase.", this);
                enabled = false;
                return;
            }

            Model = new FlowModel(Level);
            _routes.Clear();
            _routes.AddRange(builder.Build(Level));
            _lastReached = new ColorType[_routes.Count];
            FrameCamera();

            MovesLeft = Level.maxMoves;
            MovesUsed = 0;
            RefreshViews(snap: true);

            Started?.Invoke();
            MovesChanged?.Invoke(MovesLeft);

            for (var index = 0; index < _routes.Count; index++)
                _lastReached[index] = Model.ChannelColor(index);

            // A block matching its own channel at the start melts right away.
            if (Model.FindMeltable().Count > 0)
                StartCoroutine(Resolve());
        }

        public bool CanPour(int from, int to) =>
            !IsBusy && MovesLeft > 0 && Model.CanPour(from, to) && Model.TargetOf(from) != to;

        public bool TryPour(int from, int to)
        {
            if (!CanPour(from, to)) return false;

            Model.Pour(from, to);
            MovesLeft--;
            MovesUsed++;
            RefreshViews(snap: false);

            MovesChanged?.Invoke(MovesLeft);
            Poured?.Invoke(new Move(from, to));

            // Input stays free for set-up pours; it only locks while blocks
            // melt or when the last move has been spent.
            if (Model.FindMeltable().Count > 0 || MovesLeft <= 0)
                StartCoroutine(Resolve());
            else
                StartCoroutine(NudgeAfterFlow());
            return true;
        }

        /// <summary>Next move of the shortest solution from the current board.</summary>
        public Move? FindHint()
        {
            if (IsBusy) return null;
            var solution = LevelSolver.Solve(Model.Clone());
            return solution != null && solution.Count > 0 ? solution[0] : (Move?)null;
        }

        /// <summary>Adds moves after running out (continue booster).</summary>
        public void Continue(int extraMoves)
        {
            if (!_finished || Model.AllMelted) return;

            _finished = false;
            MovesLeft += extraMoves;
            MovesChanged?.Invoke(MovesLeft);
            Continued?.Invoke();
        }

        private IEnumerator Resolve()
        {
            _resolving = true;
            yield return new WaitForSeconds(flowDelay);

            while (true)
            {
                NudgeWrongBlocks();

                var meltable = Model.FindMeltable();
                if (meltable.Count == 0) break;

                foreach (var block in meltable)
                {
                    Model.Melt(block);
                    var route = _routes[block.route];
                    var view = route.Blocks[block.index];
                    view.Melt();
                    Progress.AddCoins(GameConfig.Instance.coinsPerBlock);
                    Haptics.Impact();
                    BlockMelted?.Invoke(route, view);
                }

                yield return new WaitForSeconds(meltTime);

                // Rule: after a melt every source returns to its own channel.
                Model.ResetTargets();
                RefreshViews(snap: false);
                yield return new WaitForSeconds(flowDelay);
            }

            _resolving = false;

            if (Model.AllMelted)
                Win();
            else if (MovesLeft <= 0)
                Lose();
        }

        private IEnumerator NudgeAfterFlow()
        {
            yield return new WaitForSeconds(flowDelay);
            NudgeWrongBlocks();
        }

        private void Win()
        {
            _finished = true;
            var config = GameConfig.Instance;
            var stars = config.StarsFor(MovesUsed, Level.optimalMoves);
            var reward = config.coinsPerWin + config.coinsPerStar * stars;
            Progress.AddCoins(reward);
            Progress.CompleteLevel(LevelIndex, stars);
            Won?.Invoke(new LevelResult(stars, MovesUsed, reward));
        }

        private void Lose()
        {
            _finished = true;
            Lost?.Invoke();
        }

        private void RefreshViews(bool snap)
        {
            for (var index = 0; index < _routes.Count; index++)
            {
                var route = _routes[index];
                var firstIntact = Model.FirstIntactBlock(index);
                route.Channel.SetFillLimit(firstIntact >= 0 ? route.FillLimit(firstIntact) : 1f);
                route.Channel.SetPaint(Model.ChannelColor(index));

                var target = Model.TargetOf(index);
                route.ShowPour(target != index ? _routes[target] : null);

                if (snap)
                    route.Channel.SnapToTarget();
            }
        }

        private void NudgeWrongBlocks()
        {
            for (var index = 0; index < _routes.Count; index++)
            {
                var color = Model.ChannelColor(index);
                var block = Model.FirstIntactBlock(index);
                if (block >= 0 && !color.IsEmpty() && color != _lastReached[index] &&
                    color != Model.BlockColor(new BlockRef(index, block)))
                    _routes[index].Blocks[block].Nudge();
                _lastReached[index] = color;
            }
        }

        /// <summary>Pulls the camera back so wider boards still fit the screen.</summary>
        private void FrameCamera()
        {
            if (gameCamera == null || builder == null) return;

            var spacing = builder.RouteSpacing;
            var framedWidth = (framedRouteCount - 1) * spacing + spacing;
            var width = (_routes.Count - 1) * spacing + spacing;
            var factor = Mathf.Max(1f, width / framedWidth);

            var center = builder.BoardCenterWorld;
            gameCamera.transform.position = center + (_cameraRestPosition - center) * factor;
        }
    }
}
