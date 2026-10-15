using System.Collections.Generic;
using ColorMelt.Core;
using ColorMelt.Gameplay;
using ColorMelt.Meta;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ColorMelt.UI
{
    /// <summary>
    /// Fills the level grid from the LevelDatabase using a template button:
    /// number, earned stars, a locked look for levels not reached yet and
    /// their own colour and icon for Hard and Boss levels.
    /// </summary>
    public class LevelSelectUI : MonoBehaviour
    {
        [SerializeField] private RectTransform grid;
        [Tooltip("Optional: scrolls the grid when there are more rows than fit the window.")]
        [SerializeField] private ScrollRect scroll;
        [Tooltip("Room left under the last row for its stars.")]
        [SerializeField, Min(0f)] private float bottomPadding = 80f;
        [Tooltip("Inactive button used as the template. Needs a TMP label child.")]
        [SerializeField] private Button template;
        [SerializeField] private Sprite starOn;
        [SerializeField] private Sprite starOff;
        [SerializeField] private Button closeButton;
        [SerializeField] private UIWindow window;
        [SerializeField, Range(0f, 1f)] private float lockedAlpha = 0.4f;
        [Tooltip("Sprite for levels not reached yet. Without it they are faded by lockedAlpha.")]
        [SerializeField] private Sprite lockedSprite;

        [Header("Hard and Boss levels")]
        [SerializeField] private Sprite hardSprite;
        [SerializeField] private Sprite hardIcon;
        [SerializeField] private Sprite bossSprite;
        [SerializeField] private Sprite bossIcon;
        [Tooltip("White round badge the Hard/Boss icon sits on.")]
        [SerializeField] private Sprite badgeSprite;

        private readonly List<Button> _buttons = new List<Button>();

        private void Awake()
        {
            closeButton?.onClick.AddListener(() => window?.Close());
            if (template != null) template.gameObject.SetActive(false);
            if (window != null) window.Opened += Build;
        }

        private void Start() => Build();

        private void Build()
        {
            foreach (var button in _buttons)
                Destroy(button.gameObject);
            _buttons.Clear();

            if (template == null || grid == null) return;

            for (var index = 0; index < GameSession.LevelCount; index++)
            {
                var levelIndex = index;
                var unlocked = index <= Progress.UnlockedLevel;
                var button = Instantiate(template, grid);
                button.name = $"Level {index + 1}";
                button.gameObject.SetActive(true);
                button.interactable = unlocked;

                var label = button.GetComponentInChildren<TMP_Text>();
                if (label != null) label.text = (index + 1).ToString();

                // Unity's fake-null objects break '??', so check explicitly.
                var group = button.GetComponent<CanvasGroup>();
                if (group == null) group = button.gameObject.AddComponent<CanvasGroup>();
                var showLock = !unlocked && lockedSprite != null && button.image != null;
                if (showLock) button.image.sprite = lockedSprite;
                group.alpha = unlocked || showLock ? 1f : lockedAlpha;
                MarkDifficulty(button, index, unlocked);

                AddStars(button.transform, Progress.GetStars(index), unlocked);
                button.onClick.AddListener(() => GameSession.PlayLevel(levelIndex));
                _buttons.Add(button);
            }

            CentreRows();
        }

        /// <summary>
        /// Lays the buttons out like the grid's GridLayoutGroup, but centres a
        /// short last row instead of leaving it flush left.
        /// </summary>
        private void CentreRows()
        {
            var layout = grid.GetComponent<GridLayoutGroup>();
            if (layout == null || layout.constraint != GridLayoutGroup.Constraint.FixedColumnCount) return;
            layout.enabled = false;

            var columns = layout.constraintCount;
            var cell = layout.cellSize;
            var step = cell + layout.spacing;
            var rows = (_buttons.Count + columns - 1) / columns;
            if (scroll != null)
            {
                // The grid is the scroll content: as tall as its rows.
                grid.sizeDelta = new Vector2(grid.sizeDelta.x,
                    layout.padding.top + rows * cell.y + Mathf.Max(0, rows - 1) * layout.spacing.y + bottomPadding);
            }
            for (var index = 0; index < _buttons.Count; index++)
            {
                var row = index / columns;
                var inRow = Mathf.Min(columns, _buttons.Count - row * columns);
                var rect = (RectTransform)_buttons[index].transform;
                rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.sizeDelta = cell;
                rect.anchoredPosition = new Vector2((index % columns - (inRow - 1) * 0.5f) * step.x,
                    -layout.padding.top - cell.y * 0.5f - row * step.y);
            }

            ScrollToCurrent(columns, step.y);
        }

        /// <summary>Scrolls so the furthest unlocked level is in view.</summary>
        private void ScrollToCurrent(int columns, float rowStep)
        {
            if (scroll == null || scroll.viewport == null) return;
            Canvas.ForceUpdateCanvases();
            var overflow = grid.rect.height - scroll.viewport.rect.height;
            if (overflow <= 0f)
            {
                scroll.verticalNormalizedPosition = 1f;
                return;
            }
            var row = Mathf.Min(Progress.UnlockedLevel, _buttons.Count - 1) / columns;
            var target = Mathf.Clamp(row * rowStep - scroll.viewport.rect.height * 0.35f, 0f, overflow);
            scroll.verticalNormalizedPosition = 1f - target / overflow;
        }

        private void MarkDifficulty(Button button, int index, bool unlocked)
        {
            var database = LevelDatabase.Instance;
            var data = database != null ? database.Get(index) : null;
            if (data == null || data.difficulty == LevelDifficulty.Normal) return;

            var boss = data.difficulty == LevelDifficulty.Boss;
            var sprite = boss ? bossSprite : hardSprite;
            if (unlocked && sprite != null && button.image != null) button.image.sprite = sprite;

            var iconSprite = boss ? bossIcon : hardIcon;
            if (iconSprite == null) return;

            // A white badge on the tile's corner, with the glyph tinted in the tile's colour.
            var badge = new GameObject("Difficulty", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            var badgeRect = badge.rectTransform;
            badgeRect.SetParent(button.transform, false);
            badgeRect.anchorMin = badgeRect.anchorMax = new Vector2(1f, 1f);
            badgeRect.sizeDelta = new Vector2(76f, 76f);
            badgeRect.anchoredPosition = new Vector2(-8f, -8f);
            badge.sprite = badgeSprite;
            badge.type = Image.Type.Sliced;
            badge.raycastTarget = false;

            var icon = new GameObject("Icon", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            var rect = icon.rectTransform;
            rect.SetParent(badgeRect, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(10f, 12f);
            rect.offsetMax = new Vector2(-10f, -8f);
            icon.sprite = iconSprite;
            icon.preserveAspect = true;
            icon.raycastTarget = false;
            icon.color = boss ? new Color(0.45f, 0.27f, 0.9f) : new Color(0.95f, 0.22f, 0.38f);
            if (!unlocked) badge.color = new Color(1f, 1f, 1f, 0.7f);
        }

        private void AddStars(Transform parent, int earned, bool unlocked)
        {
            if (starOn == null || !unlocked) return;

            var row = new GameObject("Stars", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            var rect = (RectTransform)row.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.sizeDelta = new Vector2(0f, 60f);
            rect.anchoredPosition = new Vector2(0f, 10f);
            var layout = row.GetComponent<HorizontalLayoutGroup>();
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.spacing = -6f;
            layout.childControlWidth = layout.childControlHeight = false;

            for (var star = 0; star < 3; star++)
            {
                var image = new GameObject("Star", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
                image.transform.SetParent(rect, false);
                image.sprite = star < earned ? starOn : starOff;
                image.raycastTarget = false;
                image.rectTransform.sizeDelta = new Vector2(56f, 56f);
            }
        }
    }
}
