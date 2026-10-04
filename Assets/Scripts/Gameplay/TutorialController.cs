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

        private void OnEnable() => level.Started += OnLevelStarted;

        private void OnDisable()
        {
            level.Started -= OnLevelStarted;
            level.Poured -= OnPoured;
            level.Poured -= OnIntroPoured;
            IsRunning = false;
        }

        private void OnLevelStarted()
        {
            if (overlay == null) return;
            if (!level.Level.tutorial)
            {
                ShowNewColours();
                return;
            }

            var solution = LevelSolver.Solve(level.Level);
            if (solution == null || solution.Count == 0) return;

            level.Poured += OnPoured;
            StartCoroutine(Run(solution));
        }

        private void OnPoured(Move move) => _poured = true;

        private void ShowNewColours()
        {
            var fresh = NewBlockColours(level.Level, level.LevelIndex);
            if (fresh.Count == 0) return;

            var recipes = new List<string>();
            foreach (var color in fresh)
                recipes.Add(color.RichRecipe());
            var message = (fresh.Count == 1 ? Localization.Get("tutorial.new_colour") + "\n" : "") + string.Join("\n", recipes);
            // Three lines only fit the banner slightly smaller.
            overlay.ShowMessage(fresh.Count > 2 ? $"<size=85%>{message}</size>" : message);

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

        /// <summary>Mixed block colours of this level that no earlier level has.</summary>
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
                            seen.Add(block.color);
                }

            var fresh = new List<ColorType>();
            foreach (var route in current.routes)
                foreach (var block in route.blocks)
                    if (fresh.Count < MaxIntroColours && block.color.Recipe().Count > 1 && seen.Add(block.color))
                        fresh.Add(block.color);
            return fresh;
        }

        private IEnumerator Run(List<Move> solution)
        {
            IsRunning = true;
            overlay.ShowMessage(Localization.Get("tutorial.intro"));
            yield return new WaitForSeconds(1.6f);

            yield return TeachCameraPad();

            foreach (var move in solution)
            {
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
                    overlay.ShowMessage(Localization.Format("tutorial.tap_channel", from.SourceColor.RichName()));
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

                var melts = preview.FindMeltable().Count > 0;
                var meltsText = Localization.Format("tutorial.melts", mixed.RichName());
                if (melts && !targetPaint.IsEmpty() && mixed != targetPaint)
                    overlay.ShowMessage(Localization.Format("tutorial.mix", from.SourceColor.RichName(),
                        targetPaint.RichName(), mixed.RichName()) + "\n" + meltsText);
                else if (melts)
                    overlay.ShowMessage(meltsText);
                else
                    overlay.ShowMessage(Localization.Get("tutorial.keep_going"));

                yield return new WaitForSeconds(0.2f);
                yield return new WaitUntil(() => !level.IsBusy || level.Model.AllMelted);
                yield return new WaitForSeconds(0.8f);
            }

            input.TapFilter = null;
            overlay.Hide();
            IsRunning = false;
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
