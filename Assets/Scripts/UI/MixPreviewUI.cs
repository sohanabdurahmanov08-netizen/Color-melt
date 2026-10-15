using System.Collections.Generic;
using ColorMelt.Core;
using ColorMelt.Gameplay;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ColorMelt.UI
{
    /// <summary>
    /// While paint is picked up, a drop over every channel it can pour into
    /// shows the colour that channel would get, with its name, so nobody is
    /// surprised by a mix. It never says whether a block would melt: that
    /// stays the player's call.
    /// </summary>
    public class MixPreviewUI : MonoBehaviour
    {
        [SerializeField] private LevelController level;
        [SerializeField] private RouteInput input;
        [SerializeField] private Sprite dropSprite;
        [SerializeField] private Sprite labelSprite;
        [SerializeField] private TMP_FontAsset font;
        [SerializeField] private Material fontMaterial;
        [Tooltip("Where on the channel the marker sits: 0 = inlet, 1 = outlet.")]
        [SerializeField, Range(0f, 1f)] private float channelPoint = 0.2f;
        [SerializeField] private Vector2 dropSize = new Vector2(120f, 120f);

        private readonly List<Marker> _markers = new List<Marker>();
        private RectTransform _area;

        private sealed class Marker
        {
            public RectTransform root;
            public Image drop;
            public TMP_Text label;
            public RouteView route;
            public float shown;
        }

        private void Awake() => _area = (RectTransform)transform;

        private void OnEnable()
        {
            input.Selected += OnSelected;
            input.Deselected += HideAll;
            level.Poured += OnPoured;
        }

        private void OnDisable()
        {
            input.Selected -= OnSelected;
            input.Deselected -= HideAll;
            level.Poured -= OnPoured;
        }

        private void OnPoured(Move move) => HideAll();

        private void OnSelected(RouteView from)
        {
            HideAll();
            var used = 0;
            foreach (var route in level.Routes)
            {
                var to = route.Index;
                var returning = to == from.Index && level.Model.TargetOf(from.Index) != from.Index;
                if (!returning && !level.CanPour(from.Index, to)) continue;

                var preview = level.Model.Clone();
                preview.Pour(from.Index, to);
                var color = preview.ChannelColor(to);
                if (color.IsEmpty()) continue;

                var marker = GetMarker(used++);
                marker.route = route;
                marker.drop.color = color.ToUnityColor();
                marker.label.text = color.DisplayName();
                marker.shown = 0f;
                marker.root.gameObject.SetActive(true);
            }
            enabled = true;
        }

        private void HideAll()
        {
            foreach (var marker in _markers)
            {
                marker.root.gameObject.SetActive(false);
                marker.route = null;
            }
        }

        private void LateUpdate()
        {
            var cam = level.Camera;
            if (cam == null) return;

            foreach (var marker in _markers)
            {
                if (marker.route == null) continue;

                var screen = cam.WorldToScreenPoint(marker.route.Channel.GetPoint(channelPoint));
                if (RectTransformUtility.ScreenPointToLocalPointInRectangle(_area, screen, null, out var local))
                    marker.root.anchoredPosition = local + Vector2.up * (6f * Mathf.Sin(Time.unscaledTime * 5f));

                // Pop in with a small overshoot.
                marker.shown = Mathf.MoveTowards(marker.shown, 1f, Time.unscaledDeltaTime * 6f);
                var t = marker.shown;
                marker.root.localScale = Vector3.one * (t < 1f ? Mathf.Sin(t * Mathf.PI * 0.75f) / 0.7071f : 1f);
            }
        }

        private Marker GetMarker(int index)
        {
            while (_markers.Count <= index)
                _markers.Add(CreateMarker());
            return _markers[index];
        }

        private Marker CreateMarker()
        {
            var root = new GameObject("Mix preview", typeof(RectTransform)).GetComponent<RectTransform>();
            root.SetParent(_area, false);
            root.anchorMin = root.anchorMax = new Vector2(0.5f, 0.5f);
            root.sizeDelta = dropSize;

            var drop = new GameObject("Drop", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            drop.transform.SetParent(root, false);
            drop.sprite = dropSprite;
            drop.preserveAspect = true;
            drop.raycastTarget = false;
            drop.rectTransform.sizeDelta = dropSize;

            var plate = new GameObject("Label", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            plate.transform.SetParent(root, false);
            plate.sprite = labelSprite;
            plate.type = Image.Type.Sliced;
            plate.raycastTarget = false;
            var plateRect = plate.rectTransform;
            plateRect.anchoredPosition = new Vector2(0f, -dropSize.y * 0.62f);
            plateRect.sizeDelta = new Vector2(250f, 64f);

            var label = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
            label.transform.SetParent(plateRect, false);
            label.rectTransform.anchorMin = Vector2.zero;
            label.rectTransform.anchorMax = Vector2.one;
            label.rectTransform.offsetMin = new Vector2(14f, 4f);
            label.rectTransform.offsetMax = new Vector2(-14f, -2f);
            if (font != null) label.font = font;
            if (fontMaterial != null) label.fontSharedMaterial = fontMaterial;
            label.enableAutoSizing = true;
            label.fontSizeMin = 18f;
            label.fontSizeMax = 36f;
            label.alignment = TextAlignmentOptions.Center;
            label.color = new Color(0x2B / 255f, 0x23 / 255f, 0x50 / 255f);
            label.raycastTarget = false;

            root.gameObject.SetActive(false);
            return new Marker { root = root, drop = drop, label = label };
        }
    }
}
