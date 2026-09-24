using System.Collections;
using ColorMelt.Gameplay;
using ColorMelt.Meta;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ColorMelt.UI
{
    /// <summary>
    /// Defeat window with a second chance: extra moves for coins, or for a
    /// rewarded ad when the player cannot afford them.
    /// </summary>
    public class LoseScreenUI : MonoBehaviour
    {
        [SerializeField] private LevelController level;
        [SerializeField] private UIWindow window;
        [SerializeField, Min(0f)] private float showDelay = 0.5f;

        [SerializeField] private Button retryButton;
        [SerializeField] private Button menuButton;
        [SerializeField] private Button continueButton;
        [SerializeField] private TMP_Text continueLabel;

        private void Awake()
        {
            retryButton?.onClick.AddListener(GameSession.Restart);
            menuButton?.onClick.AddListener(GameSession.ToMenu);
            continueButton?.onClick.AddListener(Continue);
        }

        private void OnEnable() => level.Lost += OnLost;
        private void OnDisable() => level.Lost -= OnLost;

        private void OnLost() => StartCoroutine(ShowRoutine());

        private IEnumerator ShowRoutine()
        {
            yield return new WaitForSeconds(showDelay);
            RefreshContinue();
            window.Open();
            AudioManager.PlayLose();
        }

        private void RefreshContinue()
        {
            if (continueLabel == null) return;
            var config = GameConfig.Instance;
            continueLabel.text = Progress.Coins >= config.continueCost
                ? $"+{config.continueMoves} MOVES\n<size=70%>{config.continueCost} coins</size>"
                : $"+{config.continueMoves} MOVES\n<size=70%>watch ad</size>";
        }

        private void Continue()
        {
            var config = GameConfig.Instance;
            if (Progress.TrySpendCoins(config.continueCost))
            {
                Resume(config.continueMoves);
                return;
            }

            RewardedAds.Show("lose_continue", () => Resume(config.continueMoves),
                () => Toast.Show("Ad not available"));
        }

        private void Resume(int moves)
        {
            window.Close();
            level.Continue(moves);
        }
    }
}
