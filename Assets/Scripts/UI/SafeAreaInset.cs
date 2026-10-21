using System.Collections.Generic;
using UnityEngine;

namespace ColorMelt.UI
{
    /// <summary>
    /// Keeps the HUD clear of the notch, camera cutout and home indicator.
    /// Put it on a canvas: every direct child anchored to the top edge moves
    /// down by the top inset of Screen.safeArea, every child anchored to the
    /// bottom edge moves up by the bottom inset. Full-screen children
    /// (windows, backdrops) are left alone so they still cover the screen.
    /// </summary>
    [DisallowMultipleComponent]
    public class SafeAreaInset : MonoBehaviour
    {
        [Tooltip("Children that must not move, e.g. a background.")]
        [SerializeField] private RectTransform[] ignore;
        [Tooltip("Extra canvas units added to a non-zero inset, so buttons do not sit right on the edge.")]
        [SerializeField, Min(0f)] private float margin = 12f;

        private readonly Dictionary<RectTransform, Vector2> _basePositions = new Dictionary<RectTransform, Vector2>();
        private Rect _appliedArea;
        private Vector2Int _appliedScreen;
        private float _appliedScale;
        private Canvas _canvas;

        /// <summary>Bottom inset as a share of the screen height (0 on screens without one).</summary>
        public static float BottomShare => Screen.height > 0 ? Screen.safeArea.yMin / Screen.height : 0f;

        private void Awake()
        {
            _canvas = GetComponent<Canvas>();
            foreach (RectTransform child in transform)
                if (child != null && !IsIgnored(child))
                    _basePositions[child] = child.anchoredPosition;
        }

        private void OnEnable() => Apply();

        private void Update()
        {
            // Cheap check; the safe area only changes on rotation or a resized
            // window, and the canvas scale settles a frame after loading.
            if (Screen.safeArea != _appliedArea || Screen.width != _appliedScreen.x || Screen.height != _appliedScreen.y
                || (_canvas != null && !Mathf.Approximately(_canvas.scaleFactor, _appliedScale)))
                Apply();
        }

        private void Apply()
        {
            _appliedArea = Screen.safeArea;
            _appliedScreen = new Vector2Int(Screen.width, Screen.height);
            if (Screen.height <= 0) return;

            _appliedScale = _canvas != null ? _canvas.scaleFactor : 1f;
            var scale = _appliedScale > 0f ? _appliedScale : 1f;
            var top = Inset(Screen.height - _appliedArea.yMax, scale);
            var bottom = Inset(_appliedArea.yMin, scale);

            foreach (var pair in _basePositions)
            {
                var rect = pair.Key;
                if (rect == null) continue;
                var offset = 0f;
                if (rect.anchorMin.y >= 0.999f && rect.anchorMax.y >= 0.999f) offset = -top;
                else if (rect.anchorMin.y <= 0.001f && rect.anchorMax.y <= 0.001f) offset = bottom;
                rect.anchoredPosition = pair.Value + new Vector2(0f, offset);
            }
        }

        private float Inset(float pixels, float scale) => pixels > 0.5f ? pixels / scale + margin : 0f;

        private bool IsIgnored(RectTransform child)
        {
            if (ignore == null) return false;
            foreach (var item in ignore)
                if (item == child)
                    return true;
            return false;
        }
    }
}
