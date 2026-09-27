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
        [Tooltip("Shows how close the player got, e.g. \"Only 2 blocks left!\"")]
        [SerializeField] private TMP_Text detailText;

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
            RefreshDetail();
            window.Open();
            AudioManager.PlayLose();
        }

        private void RefreshContinue()
        {
            if (continueLabel == null) return;
            var config = GameConfig.Instance;
            var price = Progress.Coins >= config.continueCost
                ? Localization.Plural("count.coins", config.continueCost)
                : Localization.Get("common.watch_ad");
            continueLabel.text = $"{Localization.Plural("count.moves_reward", config.continueMoves)}\n<size=70%>{price}</size>";
        }

        private void RefreshDetail()
        {
            if (detailText == null) return;
            var left = level.Model.IntactBlockCount;
            detailText.text = $"{Localization.Get("lose.out_of_moves")}\n" +
                              $"<size=75%>{Localization.Plural("lose.blocks_left", left)}</size>";
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
                () => Toast.Show(Localization.Get("toast.ad_unavailable")));
        }

        private void Resume(int moves)
        {
            window.Close();
            level.Continue(moves);
        }
    }
}
