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
        [Tooltip("Screen share kept free around the board for the HUD (viewport fractions).")]
        [SerializeField, Range(0f, 0.4f)] private float framePaddingTop = 0.16f;
        [SerializeField, Range(0f, 0.4f)] private float framePaddingBottom = 0.12f;
        [SerializeField, Range(0f, 0.4f)] private float framePaddingSides = 0.05f;

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
        public CameraPan Pan { get; private set; }

        /// <summary>True once the camera has been framed for the level.</summary>
        public bool IsFramed { get; private set; }
        /// <summary>Camera position that shows the whole board.</summary>
        public Vector3 FramedPosition { get; private set; }
        public Bounds BoardBounds { get; private set; }
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

            Pan = GetComponent<CameraPan>();
            if (Pan == null) Pan = gameObject.AddComponent<CameraPan>();
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
                StartCoroutine(Resolve(fromPour: false));
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
            Achievements.Add(AchievementStat.Pours);

            // Input stays free for set-up pours; it only locks while blocks
            // melt or when the last move has been spent.
            if (Model.FindMeltable().Count > 0 || MovesLeft <= 0)
                StartCoroutine(Resolve(fromPour: true));
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

        private IEnumerator Resolve(bool fromPour)
        {
            _resolving = true;
            yield return new WaitForSeconds(flowDelay);

            // Blocks melted by this pour, including the cascade after it.
            var chain = 0;

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
                chain += meltable.Count;
                Achievements.Add(AchievementStat.BlocksMelted, meltable.Count);

                yield return new WaitForSeconds(meltTime);

                // Rule: after a melt every source returns to its own channel.
                Model.ResetTargets();
                RefreshViews(snap: false);
                yield return new WaitForSeconds(flowDelay);
            }

            _resolving = false;
            if (fromPour && chain > 0)
                Achievements.ReportBest(AchievementStat.BestChain, chain);

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
            // Also re-checks the level and star achievements.
            Achievements.ReportBest(AchievementStat.MovesLeftAtWin, MovesLeft);
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

        /// <summary>
        /// Moves the camera (keeping its rotation) so the whole board is centred
        /// in the screen area left free by the HUD, pulling back as far as wide
        /// boards need. The board is never shown larger than in the scene's
        /// camera pose.
        /// </summary>
        private void FrameCamera()
        {
            if (gameCamera == null || gameCamera.orthographic) return;

            var hasBounds = false;
            var board = default(Bounds);
            foreach (var route in _routes)
            {
                if (!route.TryGetWorldBounds(out var bounds)) continue;
                if (hasBounds) board.Encapsulate(bounds);
                else board = bounds;
                hasBounds = true;
            }
            if (!hasBounds) return;

            // Work in the camera's axes: x right, y up, z forward.
            var rotation = gameCamera.transform.rotation;
            var toCamera = Quaternion.Inverse(rotation);
            var corners = new Vector3[8];
            for (var i = 0; i < 8; i++)
                corners[i] = toCamera * new Vector3(
                    (i & 1) == 0 ? board.min.x : board.max.x,
                    (i & 2) == 0 ? board.min.y : board.max.y,
                    (i & 4) == 0 ? board.min.z : board.max.z);

            var tanY = Mathf.Tan(gameCamera.fieldOfView * 0.5f * Mathf.Deg2Rad);
            var tanX = tanY * gameCamera.aspect;

            // Free screen area in normalized device coordinates (-1..1).
            var left = framePaddingSides * 2f - 1f;
            var right = 1f - framePaddingSides * 2f;
            var bottomPadding = Mathf.Max(framePaddingBottom, Pan != null ? Pan.ReservedBottom : 0f);
            var bottom = bottomPadding * 2f - 1f;
            var top = 1f - framePaddingTop * 2f;

            // A point p is on screen inside [lo, hi] when
            // lo * tan * (p.z - cam.z) <= p.axis - cam.axis <= hi * tan * (p.z - cam.z).
            // These are linear in the camera position, so the closest pose that
            // fits every corner can be solved per axis.
            float RequiredBack(Func<Vector3, float> axis, float tan, float lo, float hi)
            {
                var maxHi = float.MinValue;
                var minLo = float.MaxValue;
                foreach (var p in corners)
                {
                    maxHi = Mathf.Max(maxHi, axis(p) - hi * tan * p.z);
                    minLo = Mathf.Min(minLo, axis(p) - lo * tan * p.z);
                }
                return (maxHi - minLo) / ((hi - lo) * tan);
            }

            float Centered(Func<Vector3, float> axis, float tan, float lo, float hi, float back)
            {
                var maxHi = float.MinValue;
                var minLo = float.MaxValue;
                foreach (var p in corners)
                {
                    maxHi = Mathf.Max(maxHi, axis(p) - hi * tan * (p.z + back));
                    minLo = Mathf.Min(minLo, axis(p) - lo * tan * (p.z + back));
                }
                return (maxHi + minLo) * 0.5f;
            }

            // "back" is minus the camera's z in camera axes.
            var back = Mathf.Max(
                RequiredBack(p => p.x, tanX, left, right),
                RequiredBack(p => p.y, tanY, bottom, top));

            // Never closer to the board than the pose set up in the scene.
            var centerZ = (toCamera * board.center).z;
            var restDepth = centerZ - (toCamera * _cameraRestPosition).z;
            back = Mathf.Max(back, restDepth - centerZ);

            var position = new Vector3(
                Centered(p => p.x, tanX, left, right, back),
                Centered(p => p.y, tanY, bottom, top, back),
                -back);
            FramedPosition = rotation * position;
            BoardBounds = board;
            IsFramed = true;
            gameCamera.transform.position = FramedPosition;
        }
    }
}
