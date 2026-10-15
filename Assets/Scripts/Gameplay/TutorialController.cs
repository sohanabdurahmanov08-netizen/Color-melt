using System.Collections;
using System.Collections.Generic;
using ColorMelt.Core;
using ColorMelt.Meta;
using ColorMelt.UI;
using UnityEngine;

namespace ColorMelt.Gameplay
{
    /// <summary>
    /// Guided first level: walks the player through the solver's optimal
    /// solution one tap at a time. Only the channel the step asks for can be
    /// tapped, a pointer shows where, and each mix is explained. Enabled for
    /// every LevelData with "Tutorial" ticked.
    ///
    /// Other levels that bring in block colours not seen on earlier levels
    /// open with a short banner showing their recipes, e.g. RED + WHITE = PINK.
    /// The first level with layered blocks explains them the same way.
    /// </summary>
    public class TutorialController : MonoBehaviour
    {
        private const int MaxIntroColours = 3;
        private const float IntroDuration = 4f;

        [SerializeField] private LevelController level;
        [SerializeField] private RouteInput input;
        [SerializeField] private TutorialOverlay overlay;

        private bool _poured;
        private Coroutine _intro;

        public static bool IsRunning { get; private set; }

        private void OnEnable()
        {
            level.Started += OnLevelStarted;
            level.Won += OnWon;
        }

        /// <summary>The win sequence takes over the screen: drop the banner and pointer at once.</summary>
        private void OnWon(LevelResult result)
        {
            StopAllCoroutines();
            level.Poured -= OnPoured;
            level.Poured -= OnIntroPoured;
            _intro = null;
            input.TapFilter = null;
            IsRunning = false;
            if (overlay != null) overlay.Hide();
        }

        private void OnDisable()
        {
            level.Started -= OnLevelStarted;
            level.Won -= OnWon;
            level.Poured -= OnPoured;
            level.Poured -= OnIntroPoured;
            IsRunning = false;
        }

        private void OnLevelStarted()
        {
            if (overlay == null) return;
            if (!level.IsGuided)
            {
                ShowNewColours();
                return;
            }

            var solution = LevelSolver.Solve(level.Level);
            if (solution == null || solution.Count == 0) return;
            // Later tutorials only show the new idea, then let the player finish.
            var steps = level.Level.tutorialSteps;
            if (steps > 0 && steps < solution.Count) solution.RemoveRange(steps, solution.Count - steps);

            level.Poured += OnPoured;
            StartCoroutine(Run(solution));
        }

        private void OnPoured(Move move) => _poured = true;

        private void ShowNewColours()
        {
            string message;
            if (FirstLayeredLevel(level.Level, level.LevelIndex))
            {
                message = Localization.Get("tutorial.layers");
            }
            else
            {
                var fresh = NewBlockColours(level.Level, level.LevelIndex);
                if (fresh.Count == 0) return;

                var recipes = new List<string>();
                foreach (var color in fresh)
                    recipes.Add(color.RichRecipe());
                message = (fresh.Count == 1 ? Localization.Get("tutorial.new_colour") + "\n" : "") + string.Join("\n", recipes);
                // Three lines only fit the banner slightly smaller.
                if (fresh.Count > 2) message = $"<size=85%>{message}</size>";
            }
            overlay.ShowMessage(message);

            level.Poured += OnIntroPoured;
            _intro = StartCoroutine(HideIntroLater());
        }

        private IEnumerator HideIntroLater()
        {
            yield return new WaitForSeconds(IntroDuration);
            _intro = null;
            HideIntro();
        }

        private void OnIntroPoured(Move move) => HideIntro();

        private void HideIntro()
        {
            level.Poured -= OnIntroPoured;
            if (_intro != null) StopCoroutine(_intro);
            _intro = null;
            overlay.Hide();
        }

        /// <summary>True if this level has layered blocks and no earlier one had.</summary>
        private static bool FirstLayeredLevel(LevelData current, int index)
        {
            if (!current.HasLayeredBlocks) return false;
            var database = LevelDatabase.Instance;
            if (database == null) return true;
            for (var earlier = 0; earlier < index && earlier < database.Count; earlier++)
            {
                var data = database.levels[earlier];
                if (data != null && data != current && data.HasLayeredBlocks) return false;
            }
            return true;
        }

        /// <summary>Mixed block colours (outer or inner) of this level that no earlier level has.</summary>
        private static List<ColorType> NewBlockColours(LevelData current, int index)
        {
            var seen = new HashSet<ColorType>();
            var database = LevelDatabase.Instance;
            if (database != null)
                for (var earlier = 0; earlier < index && earlier < database.Count; earlier++)
                {
                    var data = database.levels[earlier];
                    if (data == null || data == current) continue;
                    foreach (var route in data.routes)
                        foreach (var block in route.blocks)
                        {
                            seen.Add(block.color);
                            seen.Add(block.inner);
                        }
                }

            var fresh = new List<ColorType>();
            foreach (var route in current.routes)
                foreach (var block in route.blocks)
                    foreach (var color in new[] { block.color, block.inner })
                        if (fresh.Count < MaxIntroColours && color.Recipe().Count > 1 && seen.Add(color))
                            fresh.Add(color);
            return fresh;
        }

        private IEnumerator Run(List<Move> solution)
        {
            IsRunning = true;
            var intro = string.IsNullOrEmpty(level.Level.tutorialIntro) ? "tutorial.intro" : level.Level.tutorialIntro;
            overlay.ShowMessage(Localization.Get(intro));
            yield return new WaitForSeconds(intro == "tutorial.intro" ? 1.6f : 3f);

            // The camera pad is taught once, on the very first level.
            if (level.LevelIndex == 0) yield return TeachCameraPad();

            // Shown instead of the next step's tap instruction ("now add the second colour").
            string carryOver = null;
            for (var step = 0; step < solution.Count; step++)
            {
                var move = solution[step];
                var next = step + 1 < solution.Count ? solution[step + 1] : (Move?)null;
                var from = level.Routes[move.from];
                var to = level.Routes[move.to];

                var preview = level.Model.Clone();
                preview.Pour(move.from, move.to);
                var mixed = preview.ChannelColor(move.to);
                var targetPaint = level.Model.ChannelColor(move.to);

                _poured = false;
                while (!_poured)
                {
                    // Step 1: pick up the paint.
                    input.TapFilter = route => route == from;
                    overlay.ShowMessage(carryOver ?? Localization.Format("tutorial.tap_channel", from.SourceColor.RichName()));
                    overlay.PointAt(from.TapWorldPoint, level.Camera);
                    yield return new WaitUntil(() => input.SelectedRoute == from);

                    // Step 2: pour it into the neighbour. Cancelling returns to step 1.
                    input.TapFilter = route => route == to;
                    overlay.ShowMessage(Localization.Format("tutorial.pour_into", to.SourceColor.RichName(),
                        from.SourceColor.RichName()));
                    overlay.PointAt(to.TapWorldPoint, level.Camera);
                    yield return new WaitUntil(() => _poured || input.SelectedRoute != from);
                }

                overlay.HidePointer();
                input.TapFilter = route => false;
                carryOver = null;

                var melts = preview.FindMeltable().Count > 0;
                var meltsText = Localization.Format("tutorial.melts", mixed.RichName());
                // The next step pours a second colour into the same channel.
                var addsSecond = !melts && next.HasValue && next.Value.to == move.to && next.Value.from != move.to;
                if (melts && !targetPaint.IsEmpty() && mixed != targetPaint)
                    overlay.ShowMessage(Localization.Format("tutorial.mix", from.SourceColor.RichName(),
                        targetPaint.RichName(), mixed.RichName()) + "\n" + meltsText);
                else if (melts)
                    overlay.ShowMessage(meltsText);
                else if (addsSecond)
                {
                    carryOver = Localization.Format("tutorial.add_second", from.SourceColor.RichName(),
                        level.Routes[next.Value.from].SourceColor.RichName());
                    overlay.ShowMessage(carryOver);
                }
                else
                    overlay.ShowMessage(Localization.Get("tutorial.keep_going"));

                var before = level.Model.Clone();
                yield return new WaitForSeconds(0.2f);
                yield return new WaitUntil(() => !level.IsBusy || level.Model.AllMelted);
                yield return new WaitForSeconds(0.8f);

                // Explain what happened to layered blocks during this step.
                var layers = LayerMessage(before);
                if (layers != null)
                {
                    overlay.ShowMessage(layers);
                    yield return new WaitForSeconds(2.6f);
                }
            }

            input.TapFilter = null;
            IsRunning = false;
            if (!level.Model.AllMelted)
            {
                overlay.ShowMessage(Localization.Get("tutorial.your_turn"));
                yield return new WaitForSeconds(1.8f);
            }
            overlay.Hide();
        }

        /// <summary>
        /// Says what happened to a layered block during the last step: both
        /// layers melted, or the shell melted and the core needs another
        /// colour. Null if no layered block changed.
        /// </summary>
        private string LayerMessage(FlowModel before)
        {
            var model = level.Model;
            for (var route = 0; route < model.RouteCount; route++)
                for (var index = 0; index < model.BlockCount(route); index++)
                {
                    var block = new BlockRef(route, index);
                    if (!model.IsLayered(block) || before.IsPeeled(block)) continue;
                    if (model.IsMelted(block))
                        return Localization.Format("tutorial.layer_chain", model.OuterColor(block).RichName(),
                            model.InnerColor(block).RichName());
                    if (model.IsPeeled(block))
                        return Localization.Format("tutorial.layer_next", model.InnerColor(block).RichName());
                }
            return null;
        }

        /// <summary>Highlights the camera pad and waits until the player has dragged on it.</summary>
        private IEnumerator TeachCameraPad()
        {
            var pad = level.Pan;
            if (pad == null || !pad.isActiveAndEnabled) yield break;

            input.TapFilter = route => false;
            pad.SetHighlighted(true);
            overlay.ShowMessage(Localization.Get("tutorial.camera"));
            overlay.SwipeAcross(pad.ScreenRect);

            var start = pad.DragDistance;
            yield return new WaitUntil(() => pad.DragDistance - start > Screen.height * 0.12f);
            yield return new WaitForSeconds(0.6f);

            overlay.HidePointer();
            pad.SetHighlighted(false);
        }
    }
}
