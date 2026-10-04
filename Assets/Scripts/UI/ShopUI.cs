using ColorMelt.Meta;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ColorMelt.UI
{
    /// <summary>Coin sink and source: buy hint packs, get free coins for an ad.</summary>
    public class ShopUI : MonoBehaviour
    {
        [SerializeField] private UIWindow window;
        [SerializeField] private Button closeButton;
        [SerializeField] private TMP_Text hintsText;
        [SerializeField] private Button buyHintsButton;
        [SerializeField] private TMP_Text buyHintsLabel;
        [SerializeField] private Button freeCoinsButton;
        [SerializeField] private TMP_Text freeCoinsLabel;

        private void Awake()
        {
            closeButton?.onClick.AddListener(() => window?.Close());
            buyHintsButton?.onClick.AddListener(BuyHints);
            freeCoinsButton?.onClick.AddListener(FreeCoins);
        }

        private void OnEnable()
        {
            Progress.HintsChanged += OnChanged;
            Localization.Changed += Refresh;
            Refresh();
        }

        private void OnDisable()
        {
            Progress.HintsChanged -= OnChanged;
            Localization.Changed -= Refresh;
        }

        private void OnChanged(int _) => Refresh();

        private void Refresh()
        {
            var config = GameConfig.Instance;
            if (hintsText != null) hintsText.text = Localization.Plural("count.hints_owned", Progress.Hints);
            if (buyHintsLabel != null)
                buyHintsLabel.text = $"{Localization.Plural("count.hint_pack", config.hintPackSize)}\n" +
                                     $"<size=70%>{Localization.Plural("count.coins", config.hintPackCost)}</size>";
            if (freeCoinsLabel != null)
                freeCoinsLabel.text = $"{Localization.Plural("count.coins_reward", config.freeCoinsReward)}\n" +
                                      $"<size=70%>{Localization.Get("common.watch_ad")}</size>";
        }

        private void BuyHints()
        {
            var config = GameConfig.Instance;
            if (!Progress.TrySpendCoins(config.hintPackCost))
            {
                Toast.Show(Localization.Get("toast.not_enough_coins"));
                return;
            }

            Progress.AddHints(config.hintPackSize);
            Achievements.Add(AchievementStat.HintPacksBought);
            Toast.Show(Localization.Plural("count.hints_added", config.hintPackSize));
        }

        private void FreeCoins()
        {
            if (!RewardedAds.IsReady)
            {
                Toast.Show(Localization.Get("toast.ad_unavailable"));
                return;
            }
            RewardedAds.Show("shop_free_coins", () => Progress.AddCoins(GameConfig.Instance.freeCoinsReward));
        }
    }
}
