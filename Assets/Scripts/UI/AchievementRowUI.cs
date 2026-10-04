using System.Collections;
using ColorMelt.Meta;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ColorMelt.UI
{
    /// <summary>
    /// One line of the achievements window: icon tile, name, task, progress
    /// bar and, on the right, the coin reward (locked), a Claim button
    /// (unlocked) or a check mark (claimed).
    /// </summary>
    public class AchievementRowUI : MonoBehaviour
    {
        [SerializeField] private Image background;
        [SerializeField] private Image tile;
        [SerializeField] private Image icon;
        [SerializeField] private TMP_Text title;
        [SerializeField] private TMP_Text description;

        [Header("Progress bar")]
        [SerializeField] private RectTransform barFill;
        [SerializeField] private TMP_Text barText;

        [Header("Right side")]
        [Tooltip("Coin reward shown while the task is not done yet.")]
        [SerializeField] private GameObject rewardTag;
        [SerializeField] private TMP_Text rewardText;
        [SerializeField] private Button claimButton;
        [SerializeField] private TMP_Text claimLabel;
        [SerializeField] private TMP_Text claimAmount;
        [SerializeField] private RectTransform doneMark;

        [Header("Looks")]
        [Tooltip("Tile shown instead of the achievement's colour while it is locked.")]
        [SerializeField] private Sprite lockedTile;
        [SerializeField] private Color lockedIconColor = new Color(1f, 1f, 1f, 0.85f);
        [SerializeField] private Color claimedBackground = new Color(0.86f, 0.97f, 0.88f);

        private AchievementData _data;

        public AchievementData Data => _data;

        private void Awake() => claimButton?.onClick.AddListener(Claim);

        public void Bind(AchievementData data)
        {
            _data = data;
            Refresh();
        }

        public void Refresh()
        {
            if (_data == null) return;

            var state = Achievements.StateOf(_data);
            var target = Achievements.Target(_data);
            var current = Achievements.Current(_data);
            var locked = state == AchievementState.Locked;

            if (title != null) title.text = Localization.Get("ach." + _data.id);
            if (description != null) description.text = Description(_data, target);

            if (tile != null) tile.sprite = locked && lockedTile != null ? lockedTile : _data.tile;
            if (icon != null)
            {
                icon.sprite = _data.icon;
                icon.color = locked ? lockedIconColor : Color.white;
            }
            if (background != null) background.color = state == AchievementState.Claimed ? claimedBackground : Color.white;

            if (barFill != null)
            {
                var fraction = Mathf.Clamp01(current / (float)target);
                barFill.anchorMax = new Vector2(fraction, barFill.anchorMax.y);
                barFill.gameObject.SetActive(fraction > 0f);
            }
            if (barText != null) barText.text = $"{current}/{target}";

            if (rewardTag != null) rewardTag.SetActive(locked);
            if (rewardText != null) rewardText.text = "+" + _data.reward;
            if (claimButton != null) claimButton.gameObject.SetActive(state == AchievementState.Unlocked);
            if (claimLabel != null) claimLabel.text = Localization.Get("ach.claim");
            if (claimAmount != null) claimAmount.text = "+" + _data.reward;
            if (doneMark != null) doneMark.gameObject.SetActive(state == AchievementState.Claimed);
        }

        /// <summary>Task text, e.g. "Melt 50 blocks"; ".all" keys cover every-level targets.</summary>
        public static string Description(AchievementData data, int target)
        {
            var key = "ach.desc." + data.stat;
            if (data.allLevels && Localization.TryGet(key + ".all", out var all)) return all;
            return Localization.Plural(key, target);
        }

        private void Claim()
        {
            if (_data == null || !Achievements.Claim(_data)) return;
            Refresh();
            if (doneMark != null) StartCoroutine(Pop(doneMark));
        }

        private static IEnumerator Pop(RectTransform target)
        {
            const float duration = 0.3f;
            for (var time = 0f; time < duration; time += Time.unscaledDeltaTime)
            {
                var t = time / duration;
                target.localScale = Vector3.one * (t < 0.6f ? Mathf.Lerp(0.2f, 1.3f, t / 0.6f) : Mathf.Lerp(1.3f, 1f, (t - 0.6f) / 0.4f));
                yield return null;
            }
            target.localScale = Vector3.one;
        }
    }
}
