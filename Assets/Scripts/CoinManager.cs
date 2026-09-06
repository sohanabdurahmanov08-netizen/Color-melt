using UnityEngine;
using TMPro;

namespace ColorMelt.Core
{
    public class CoinManager : MonoBehaviour
    {
        public static CoinManager Instance { get; private set; }

        [Header("Coins")]
        [SerializeField] private int startingCoins = 0;
        [SerializeField] private int coinsPerBlock = 5;

        [Header("UI")]
        [SerializeField] private TMP_Text coinText;

        private int coins;

        public int Coins => coins;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;

            coins = startingCoins;
            UpdateUI();
        }

        public void AddCoins(int amount)
        {
            if (amount <= 0)
                return;

            coins += amount;
            UpdateUI();
        }

        public void AddCoinsForBlock()
        {
            AddCoins(coinsPerBlock);
        }

        private void UpdateUI()
        {
            if (coinText != null)
                coinText.text = coins.ToString();
        }
    }
}
