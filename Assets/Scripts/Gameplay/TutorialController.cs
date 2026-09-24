using System.Collections;
using ColorMelt.Core;
using ColorMelt.UI;
using UnityEngine;

namespace ColorMelt.Gameplay
{
    /// <summary>
    /// Guided first level: walks the player through the solver's optimal
    /// solution one tap at a time. Only the channel the step asks for can be
    /// tapped, a pointer shows where, and each mix is explained. Enabled for
    /// every LevelData with "Tutorial" ticked.
    /// </summary>
    public class TutorialController : MonoBehaviour
    {
        [SerializeField] private LevelController level;
        [SerializeField] private RouteInput input;
        [SerializeField] private TutorialOverlay overlay;

        private bool _poured;

        public static bool IsRunning { get; private set; }

        private void OnEnable() => level.Started += OnLevelStarted;

        private void OnDisable()
        {
            level.Started -= OnLevelStarted;
            level.Poured -= OnPoured;
            IsRunning = false;
        }

        private void OnLevelStarted()
        {
            if (!level.Level.tutorial || overlay == null) return;

            var solution = LevelSolver.Solve(level.Level);
            if (solution == null || solution.Count == 0) return;

            level.Poured += OnPoured;
            StartCoroutine(Run(solution));
        }

        private void OnPoured(Move move) => _poured = true;

        private IEnumerator Run(System.Collections.Generic.List<Move> solution)
        {
            IsRunning = true;
            overlay.ShowMessage("Mix paints to melt blocks\nof the same colour!");
            yield return new WaitForSeconds(1.6f);

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
                    overlay.ShowMessage($"Tap the {from.SourceColor.RichName()} channel");
                    overlay.PointAt(from.TapWorldPoint, level.Camera);
                    yield return new WaitUntil(() => input.SelectedRoute == from);

                    // Step 2: pour it into the neighbour. Cancelling returns to step 1.
                    input.TapFilter = route => route == to;
                    overlay.ShowMessage($"Now tap {to.SourceColor.RichName()} to pour\n{from.SourceColor.RichName()} into it");
                    overlay.PointAt(to.TapWorldPoint, level.Camera);
                    yield return new WaitUntil(() => _poured || input.SelectedRoute != from);
                }

                overlay.HidePointer();
                input.TapFilter = route => false;

                var melts = preview.FindMeltable().Count > 0;
                if (melts && !targetPaint.IsEmpty() && mixed != targetPaint)
                    overlay.ShowMessage($"{from.SourceColor.RichName()} + {targetPaint.RichName()} = {mixed.RichName()}!\n" +
                                        $"{mixed.RichName()} paint melts {mixed.RichName()} blocks");
                else if (melts)
                    overlay.ShowMessage($"{mixed.RichName()} paint melts {mixed.RichName()} blocks");
                else
                    overlay.ShowMessage("Nice! Keep going");

                yield return new WaitForSeconds(0.2f);
                yield return new WaitUntil(() => !level.IsBusy || level.Model.AllMelted);
                yield return new WaitForSeconds(0.8f);
            }

            input.TapFilter = null;
            overlay.Hide();
            IsRunning = false;
        }
    }
}
