using System.Collections.Generic;
using ColorMelt.Gameplay;
using ColorMelt.Meta;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ColorMelt.UI
{
    /// <summary>
    /// Fills the level grid from the LevelDatabase using a template button:
    /// number, earned stars and a locked look for levels not reached yet.
    /// </summary>
    public class LevelSelectUI : MonoBehaviour
    {
        [SerializeField] private RectTransform grid;
        [Tooltip("Inactive button used as the template. Needs a TMP label child.")]
        [SerializeField] private Button template;
        [SerializeField] private Sprite starOn;
        [SerializeField] private Sprite starOff;
        [SerializeField] private Button closeButton;
        [SerializeField] private UIWindow window;
        [SerializeField, Range(0f, 1f)] private float lockedAlpha = 0.4f;

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
                group.alpha = unlocked ? 1f : lockedAlpha;

                AddStars(button.transform, Progress.GetStars(index), unlocked);
                button.onClick.AddListener(() => GameSession.PlayLevel(levelIndex));
                _buttons.Add(button);
            }
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
