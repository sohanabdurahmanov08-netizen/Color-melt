using ColorMelt.Meta;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ColorMelt.UI
{
    /// <summary>
    /// One booster in the HUD bar: icon, name, and a badge with how many the
    /// player owns. With none left the badge shows "+" and the name line
    /// shows the coin price instead. BoosterBar decides what tapping does.
    /// </summary>
    [RequireComponent(typeof(Button))]
    public class BoosterButton : MonoBehaviour
    {
        [SerializeField] private BoosterType type;
        [SerializeField] private TMP_Text label;
        [SerializeField] private TMP_Text badgeText;
        [Tooltip("Shown instead of the icon, e.g. \"+3\" on the extra moves booster.")]
        [SerializeField] private TMP_Text iconText;
        [SerializeField, Range(0f, 1f)] private float unusableAlpha = 0.45f;

        private Button _button;
        private CanvasGroup _group;
        private bool? _usable;
        private bool _active;

        public BoosterType Type => type;
        public Button Button => _button != null ? _button : _button = GetComponent<Button>();

        /// <summary>Pulsing look while this booster waits for the player to pick a channel.</summary>
        public bool Active
        {
            get => _active;
            set
            {
                _active = value;
                if (!value) transform.localScale = Vector3.one;
            }
        }

        private void Awake()
        {
            _group = GetComponent<CanvasGroup>();
            if (_group == null) _group = gameObject.AddComponent<CanvasGroup>();
        }

        public void Refresh()
        {
            var config = GameConfig.Instance;
            var count = Progress.GetBoosters(type);
            if (badgeText != null) badgeText.text = count > 0 ? count.ToString() : "+";
            if (label != null)
                label.text = count > 0
                    ? Localization.Get("booster." + type)
                    : Localization.Plural("count.coins", config.BoosterCost(type));
            if (iconText != null && type == BoosterType.ExtraMoves)
                iconText.text = "+" + config.extraMoves;
        }

        /// <summary>Greys the button out when it cannot do anything right now. Still tappable for feedback.</summary>
        public void SetUsable(bool usable)
        {
            if (_usable == usable) return;
            _usable = usable;
            if (_group == null) Awake();
            _group.alpha = usable ? 1f : unusableAlpha;
        }

        private void Update()
        {
            if (!_active) return;
            transform.localScale = Vector3.one * (1f + 0.07f * Mathf.Sin(Time.unscaledTime * 9f));
        }
    }
}
