using ColorMelt.Meta;
using TMPro;
using UnityEngine;

namespace ColorMelt.UI
{
    /// <summary>Shows the saved coin balance, counting up with a punch when it grows.</summary>
    public class CoinCounterUI : MonoBehaviour
    {
        [SerializeField] private TMP_Text label;
        [SerializeField] private RectTransform punchTarget;
        [SerializeField, Min(0.1f)] private float countSpeed = 6f;

        private float _shown;
        private float _punch;

        private void OnEnable()
        {
            _shown = Progress.Coins;
            Progress.CoinsChanged += OnCoinsChanged;
            Refresh();
        }

        private void OnDisable() => Progress.CoinsChanged -= OnCoinsChanged;

        private void OnCoinsChanged(int coins)
        {
            if (coins > _shown)
            {
                _punch = 1f;
                AudioManager.PlayCoin();
            }
        }

        private void Update()
        {
            var target = Progress.Coins;
            if (!Mathf.Approximately(_shown, target))
            {
                // Exponential approach, but always at least one coin per frame.
                var step = Mathf.Max(1f, Mathf.Abs(target - _shown) * countSpeed * Time.unscaledDeltaTime);
                _shown = Mathf.MoveTowards(_shown, target, step);
                Refresh();
            }

            _punch = Mathf.MoveTowards(_punch, 0f, Time.unscaledDeltaTime * 4f);
            if (punchTarget != null)
                punchTarget.localScale = Vector3.one * (1f + 0.2f * Mathf.Sin(_punch * Mathf.PI));
        }

        private void Refresh()
        {
            if (label != null) label.text = Mathf.RoundToInt(_shown).ToString();
        }
    }
}
