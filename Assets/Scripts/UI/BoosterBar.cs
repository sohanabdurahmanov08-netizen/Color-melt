using System.Collections;
using System.Collections.Generic;
using ColorMelt.Core;
using ColorMelt.Gameplay;
using ColorMelt.Meta;
using UnityEngine;

namespace ColorMelt.UI
{
    /// <summary>
    /// The booster row at the bottom of the HUD: hint, undo, rainbow drop and
    /// extra moves. A booster the player owns is used for free, otherwise it
    /// costs coins. Boosters appear from the level set in GameConfig and are
    /// hidden on the tutorial level.
    ///
    /// Tips: the first time a booster appears the tutorial banner explains it
    /// while a pointer pulses over its button, until the player taps it, pours
    /// or a few seconds pass. Two one-off tips point at a booster when it would
    /// help: Undo when the last pour made the level unsolvable or wasted a move
    /// the player now lacks, and later extra moves when moves run short again.
    /// </summary>
    public class BoosterBar : MonoBehaviour
    {
        [SerializeField] private LevelController level;
        [SerializeField] private RouteInput input;
        [SerializeField] private BoosterButton[] buttons;
        [Tooltip("Banner and pointer used for the booster tips. Found in the scene if empty.")]
        [SerializeField] private TutorialOverlay overlay;
        [SerializeField, Min(1f)] private float tipDuration = 6f;

        private const string StuckTip = "undo_stuck";
        private const string UndoShortTip = "undo_short";
        private const string ShortTip = "moves_short";

        private readonly Queue<BoosterButton> _intros = new Queue<BoosterButton>();
        private BoosterButton _rainbow;
        private bool _started;
        private bool _tipShowing;
        private bool _ended;
        private bool _pouredDuringTip;
        private BoosterType? _tappedDuringTip;
        private Coroutine _introRoutine;

        private void Awake()
        {
            foreach (var button in buttons)
            {
                var booster = button;
                booster.Button.onClick.AddListener(() => Use(booster));
                if (booster.Type == BoosterType.Rainbow) _rainbow = booster;
            }
            if (overlay == null) overlay = FindAnyObjectByType<TutorialOverlay>(FindObjectsInactive.Include);
        }

        private void OnEnable()
        {
            level.Started += OnStarted;
            level.Poured += OnPoured;
            level.Won += OnWon;
            level.Lost += OnEnded;
            level.Continued += OnContinued;
            input.PickingChanged += OnPickingChanged;
            Progress.BoostersChanged += OnBoostersChanged;
            Progress.CoinsChanged += OnCoinsChanged;
            Localization.Changed += RefreshAll;
        }

        private void OnDisable()
        {
            level.Started -= OnStarted;
            level.Poured -= OnPoured;
            level.Won -= OnWon;
            level.Lost -= OnEnded;
            level.Continued -= OnContinued;
            input.PickingChanged -= OnPickingChanged;
            Progress.BoostersChanged -= OnBoostersChanged;
            Progress.CoinsChanged -= OnCoinsChanged;
            Localization.Changed -= RefreshAll;
        }

        private void OnStarted()
        {
            _started = true;
            _ended = false;
            var config = GameConfig.Instance;
            var levelNumber = level.LevelIndex + 1;
            var anyVisible = false;

            foreach (var button in buttons)
            {
                // A level guided from start to end has no use for boosters.
                var fullyGuided = level.IsGuided && level.Level.tutorialSteps == 0;
                var visible = !fullyGuided && levelNumber >= config.BoosterFromLevel(button.Type);
                button.gameObject.SetActive(visible);
                anyVisible |= visible;
                if (!visible) continue;

                if (Progress.MarkBoosterSeen(button.Type))
                    _intros.Enqueue(button);
            }

            gameObject.SetActive(anyVisible);
            RefreshAll();
            if (_intros.Count > 0 && isActiveAndEnabled) _introRoutine = StartCoroutine(ShowIntros());
        }

        // ------------------------------------------------------------ tips

        private IEnumerator ShowIntros()
        {
            // Let the level appear first, and the new-colour banner or a
            // guided tutorial finish: they share the overlay.
            while (SceneFader.IsTransitioning) yield return null;
            yield return new WaitForSeconds(0.8f);
            while (_intros.Count > 0 && !_ended)
            {
                while (!_ended && (TutorialController.IsRunning || (overlay != null && overlay.IsShowing)))
                    yield return null;
                if (_ended) break;

                var button = _intros.Dequeue();
                var text = Localization.Format("booster.intro." + button.Type, GameConfig.Instance.extraMoves);
                // Undo has nothing to take back yet, so it is not offered as "try it".
                if (button.Type != BoosterType.Undo)
                    text += "\n<size=75%>" + Localization.Get("booster.tip.try") + "</size>";
                yield return Spotlight(button, text, tipDuration);
                yield return new WaitForSeconds(0.35f);
            }
            _intros.Clear();
            _introRoutine = null;
        }

        /// <summary>Banner plus a pulsing pointer on the button, until it is tapped, a pour or a timeout.</summary>
        private IEnumerator Spotlight(BoosterButton button, string text, float duration)
        {
            if (overlay == null || !button.isActiveAndEnabled)
            {
                Toast.Show(text);
                yield break;
            }

            _tipShowing = true;
            _pouredDuringTip = false;
            _tappedDuringTip = null;
            overlay.ShowMessage(text);
            overlay.PointAtUI((RectTransform)button.transform);
            button.GetComponent<UIButtonFeedback>()?.Punch(6f);
            Haptics.Light();

            var until = Time.time + duration;
            while (!_ended && !_pouredDuringTip && _tappedDuringTip == null && Time.time < until)
                yield return null;

            EndSpotlight();
        }

        private void EndSpotlight()
        {
            if (!_tipShowing) return;
            _tipShowing = false;
            if (overlay != null && !TutorialController.IsRunning) overlay.Hide();
        }

        private void OnPoured(Move move)
        {
            if (_tipShowing) _pouredDuringTip = true;
            if (_introRoutine != null || level.IsGuided) return;
            if (Progress.IsTipSeen(StuckTip) && Progress.IsTipSeen(UndoShortTip) && Progress.IsTipSeen(ShortTip)) return;
            StartCoroutine(CheckSituationTips());
        }

        /// <summary>After the pour settles: point at Undo on a dead end, or at extra moves when short.</summary>
        private IEnumerator CheckSituationTips()
        {
            yield return null;
            var timeout = Time.time + 6f;
            // IsBusy stays true once the level is won or lost; _ended covers that.
            while (level.IsBusy && !_ended && Time.time < timeout) yield return null;
            if (_ended || level.IsBusy || _tipShowing || TutorialController.IsRunning) yield break;
            if (overlay != null && overlay.IsShowing) yield break;
            if (level.MovesLeft <= 0) yield break;

            var solution = LevelSolver.Solve(level.Model.Clone());
            if (solution == null)
            {
                var undo = Find(BoosterType.Undo);
                if (undo != null && level.CanUndo && Progress.MarkTipSeen(StuckTip))
                    yield return Spotlight(undo, Localization.Get("booster.tip.stuck"), tipDuration);
            }
            else if (solution.Count > level.MovesLeft)
            {
                // Undo gives the wasted move back, so it is suggested first;
                // the next time the player runs short, extra moves.
                var undo = Find(BoosterType.Undo);
                if (undo != null && level.CanUndo && !Progress.IsTipSeen(UndoShortTip))
                {
                    Progress.MarkTipSeen(UndoShortTip);
                    yield return Spotlight(undo, Localization.Get("booster.tip.undo_short"), tipDuration);
                    yield break;
                }

                var extra = Find(BoosterType.ExtraMoves);
                if (extra != null && level.CanAddMoves && Progress.MarkTipSeen(ShortTip))
                    yield return Spotlight(extra,
                        Localization.Format("booster.tip.short", GameConfig.Instance.extraMoves), tipDuration);
            }
        }

        private BoosterButton Find(BoosterType type)
        {
            foreach (var button in buttons)
                if (button.Type == type && button.isActiveAndEnabled)
                    return button;
            return null;
        }

        private void OnWon(LevelResult result) => OnEnded();

        private void OnEnded()
        {
            _ended = true;
            EndSpotlight();
        }

        // A continue after losing brings the level back to life.
        private void OnContinued() => _ended = false;

        private void OnBoostersChanged(BoosterType type, int count) => RefreshAll();
        private void OnCoinsChanged(int coins) => RefreshAll();

        private void RefreshAll()
        {
            foreach (var button in buttons)
                button.Refresh();
        }

        private void Update()
        {
            if (!_started) return;
            foreach (var button in buttons)
                button.SetUsable(IsUsable(button.Type));
        }

        private bool IsUsable(BoosterType type)
        {
            if (TutorialController.IsRunning || level.IsBusy) return false;
            switch (type)
            {
                case BoosterType.Hint: return level.MovesLeft > 0;
                case BoosterType.Undo: return level.CanUndo;
                case BoosterType.Rainbow: return HasRainbowTarget();
                default: return level.CanAddMoves;
            }
        }

        private bool HasRainbowTarget()
        {
            for (var route = 0; route < level.Routes.Count; route++)
                if (level.CanRainbow(route))
                    return true;
            return false;
        }

        private void Use(BoosterButton button)
        {
            if (TutorialController.IsRunning || level.IsBusy) return;
            if (_tipShowing) _tappedDuringTip = button.Type;

            // A second tap on the rainbow (or any other booster) cancels picking a channel.
            if (input.IsPicking)
            {
                input.CancelPick();
                if (button.Type == BoosterType.Rainbow) return;
            }

            switch (button.Type)
            {
                case BoosterType.Hint: UseHint(); break;
                case BoosterType.Undo: UseUndo(); break;
                case BoosterType.Rainbow: StartRainbow(); break;
                case BoosterType.ExtraMoves: UseExtraMoves(); break;
            }
        }

        private void UseHint()
        {
            var hint = level.FindHint();
            if (!hint.HasValue)
            {
                Toast.Show(Localization.Get(level.MovesLeft > 0 ? "toast.no_solution" : "toast.no_moves"));
                return;
            }
            if (!Pay(BoosterType.Hint)) return;

            input.ShowHint(hint.Value);
            Achievements.Add(AchievementStat.HintsUsed);
        }

        private void UseUndo()
        {
            if (!level.CanUndo)
            {
                Toast.Show(Localization.Get("toast.nothing_to_undo"));
                return;
            }
            if (!Pay(BoosterType.Undo)) return;

            input.ClearSelection();
            input.ClearHint();
            level.Undo();
            AudioManager.PlayPop();
            Haptics.Light();
        }

        private void StartRainbow()
        {
            if (!HasRainbowTarget()) return;
            if (!CanAfford(BoosterType.Rainbow))
            {
                Toast.Show(Localization.Get("toast.not_enough_coins"));
                return;
            }

            input.ClearHint();
            Toast.Show(Localization.Get("booster.rainbow_pick"));
            input.BeginPick(route => level.CanRainbow(route.Index), route =>
            {
                if (!level.CanRainbow(route.Index) || !Pay(BoosterType.Rainbow)) return;
                level.UseRainbow(route.Index);
            });
        }

        private void UseExtraMoves()
        {
            if (!level.CanAddMoves) return;
            if (!Pay(BoosterType.ExtraMoves)) return;

            level.AddMoves(GameConfig.Instance.extraMoves);
            AudioManager.PlayCoin();
            Haptics.Medium();
        }

        private void OnPickingChanged(bool picking)
        {
            if (_rainbow != null) _rainbow.Active = picking;
        }

        private static bool CanAfford(BoosterType type) =>
            Progress.GetBoosters(type) > 0 || Progress.Coins >= GameConfig.Instance.BoosterCost(type);

        /// <summary>Spends one owned booster, or its price in coins.</summary>
        private static bool Pay(BoosterType type)
        {
            if (Progress.GetBoosters(type) > 0)
            {
                Progress.AddBooster(type, -1);
                return true;
            }
            if (Progress.TrySpendCoins(GameConfig.Instance.BoosterCost(type)))
                return true;

            Toast.Show(Localization.Get("toast.not_enough_coins"));
            return false;
        }
    }
}
