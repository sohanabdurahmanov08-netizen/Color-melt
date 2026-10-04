using ColorMelt.Meta;
using TMPro;
using UnityEngine;

namespace ColorMelt.UI
{
    /// <summary>
    /// Red counter on the menu's Achievements button: how many rewards are
    /// waiting to be claimed. Hidden when there are none.
    /// </summary>
    public class AchievementBadgeUI : MonoBehaviour
    {
        [SerializeField] private RectTransform badge;
        [SerializeField] private TMP_Text count;

        private void OnEnable()
        {
            Achievements.Changed += Refresh;
            // Picks up achievements reached before they existed (older saves).
            Achievements.Check();
            Refresh();
        }

        private void OnDisable() => Achievements.Changed -= Refresh;

        private void Refresh()
        {
            if (badge == null) return;
            var claimable = Achievements.ClaimableCount;
            badge.gameObject.SetActive(claimable > 0);
            if (count != null) count.text = claimable.ToString();
        }

        private void Update()
        {
            if (badge == null || !badge.gameObject.activeSelf) return;
            badge.localScale = Vector3.one * (1f + 0.08f * Mathf.Sin(Time.unscaledTime * 5f));
        }
    }
}
