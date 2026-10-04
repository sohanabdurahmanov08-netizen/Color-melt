using System.Collections.Generic;
using System.Linq;
using ColorMelt.Meta;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ColorMelt.UI
{
    /// <summary>
    /// Achievements window: a scrolling list built from a template row each
    /// time it opens. Rewards waiting to be claimed come first, then tasks in
    /// progress, then finished ones.
    /// </summary>
    public class AchievementsUI : MonoBehaviour
    {
        [SerializeField] private UIWindow window;
        [SerializeField] private Button closeButton;
        [SerializeField] private TMP_Text summaryText;
        [SerializeField] private ScrollRect scroll;
        [Tooltip("Inactive row inside the scroll content, copied once per achievement.")]
        [SerializeField] private AchievementRowUI template;

        private readonly List<AchievementRowUI> _rows = new List<AchievementRowUI>();

        private void Awake()
        {
            closeButton?.onClick.AddListener(() => window?.Close());
            if (template != null) template.gameObject.SetActive(false);
            if (window != null) window.Opened += Build;
        }

        private void OnEnable()
        {
            Achievements.Changed += Refresh;
            Localization.Changed += Refresh;
        }

        private void OnDisable()
        {
            Achievements.Changed -= Refresh;
            Localization.Changed -= Refresh;
        }

        private void Build()
        {
            foreach (var row in _rows)
                Destroy(row.gameObject);
            _rows.Clear();

            Achievements.Check();
            if (template != null)
            {
                // OrderBy is stable, so each group keeps the database order.
                foreach (var data in Achievements.All.Where(a => !string.IsNullOrEmpty(a.id)).OrderBy(SortGroup))
                {
                    var row = Instantiate(template, template.transform.parent);
                    row.name = data.id;
                    row.gameObject.SetActive(true);
                    row.Bind(data);
                    _rows.Add(row);
                }
            }

            RefreshSummary();
            if (scroll != null)
            {
                Canvas.ForceUpdateCanvases();
                scroll.verticalNormalizedPosition = 1f;
            }
        }

        private static int SortGroup(AchievementData data)
        {
            switch (Achievements.StateOf(data))
            {
                case AchievementState.Unlocked: return 0;
                case AchievementState.Locked: return 1;
                default: return 2;
            }
        }

        /// <summary>Updates rows in place; the order only changes when the window reopens.</summary>
        private void Refresh()
        {
            foreach (var row in _rows)
                row.Refresh();
            RefreshSummary();
        }

        private void RefreshSummary()
        {
            if (summaryText == null) return;
            var total = Achievements.All.Count(a => !string.IsNullOrEmpty(a.id));
            summaryText.text = Localization.Format("ach.summary", Achievements.UnlockedCount, total);
        }
    }
}
