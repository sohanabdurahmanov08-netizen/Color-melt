using System.Collections;
using System.Collections.Generic;
using ColorMelt.Gameplay;
using ColorMelt.Meta;
using UnityEngine;

namespace ColorMelt.UI
{
    /// <summary>
    /// The booster row at the bottom of the HUD: hint, undo, rainbow drop and
    /// extra moves. A booster the player owns is used for free, otherwise it
    /// costs coins. Boosters appear from the level set in GameConfig, each
    /// with a one-time intro, and are hidden on the tutorial level.
    /// </summary>
    public class BoosterBar : MonoBehaviour
    {
        [SerializeField] private LevelController level;
        [SerializeField] private RouteInput input;
        [SerializeField] private BoosterButton[] buttons;

        private readonly Queue<string> _intros = new Queue<string>();
        private BoosterButton _rainbow;
        private bool _started;

        private void Awake()
        {
            foreach (var button in buttons)
            {
                var booster = button;
                booster.Button.onClick.AddListener(() => Use(booster));
                if (booster.Type == BoosterType.Rainbow) _rainbow = booster;
            }
        }

        private void OnEnable()
        {
            level.Started += OnStarted;
            input.PickingChanged += OnPickingChanged;
            Progress.BoostersChanged += OnBoostersChanged;
            Progress.CoinsChanged += OnCoinsChanged;
            Localization.Changed += RefreshAll;
        }

        private void OnDisable()
        {
            level.Started -= OnStarted;
            input.PickingChanged -= OnPickingChanged;
            Progress.BoostersChanged -= OnBoostersChanged;
            Progress.CoinsChanged -= OnCoinsChanged;
            Localization.Changed -= RefreshAll;
        }

        private void OnStarted()
        {
            _started = true;
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

                // Hints were around before the other boosters, so they need no intro.
                if (Progress.MarkBoosterSeen(button.Type) && button.Type != BoosterType.Hint)
                {
                    _intros.Enqueue(Localization.Format("booster.intro." + button.Type, config.extraMoves));
                    button.GetComponent<UIButtonFeedback>()?.Punch(6f);
                }
            }

            gameObject.SetActive(anyVisible);
            RefreshAll();
            if (_intros.Count > 0) StartCoroutine(ShowIntros());
        }

        private IEnumerator ShowIntros()
        {
            // Let the level appear (and any banner start) first.
            while (SceneFader.IsTransitioning) yield return null;
            yield return new WaitForSeconds(0.8f);
            while (_intros.Count > 0)
            {
                Toast.Show(_intros.Dequeue());
                yield return new WaitForSecondsRealtime(2.2f);
            }
        }

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
