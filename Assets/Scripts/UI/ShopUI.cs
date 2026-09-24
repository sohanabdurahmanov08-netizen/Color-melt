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
            Refresh();
        }

        private void OnDisable() => Progress.HintsChanged -= OnChanged;

        private void OnChanged(int _) => Refresh();

        private void Refresh()
        {
            var config = GameConfig.Instance;
            if (hintsText != null) hintsText.text = $"You have {Progress.Hints} hints";
            if (buyHintsLabel != null)
                buyHintsLabel.text = $"{config.hintPackSize} HINTS\n<size=70%>{config.hintPackCost} coins</size>";
            if (freeCoinsLabel != null)
                freeCoinsLabel.text = $"+{config.freeCoinsReward} COINS\n<size=70%>watch ad</size>";
        }

        private void BuyHints()
        {
            var config = GameConfig.Instance;
            if (!Progress.TrySpendCoins(config.hintPackCost))
            {
                Toast.Show("Not enough coins");
                return;
            }

            Progress.AddHints(config.hintPackSize);
            Toast.Show($"+{config.hintPackSize} hints!");
        }

        private void FreeCoins()
        {
            RewardedAds.Show("shop_free_coins", () => Progress.AddCoins(GameConfig.Instance.freeCoinsReward),
                () => Toast.Show("Ad not available"));
        }
    }
}
