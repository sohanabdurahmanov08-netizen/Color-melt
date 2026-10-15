using System.Collections;
using System.Collections.Generic;
using ColorMelt.Core;
using ColorMelt.Gameplay;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace ColorMelt.UI
{
    /// <summary>
    /// The moment between the last melt and the victory window. The HUD
    /// fades, the camera sinks along the paint pouring off every channel, a
    /// jar rises from the bottom of the screen and the paint streams fill it
    /// in layers, one per channel; the fuller the jar, the more stars. Then
    /// WinScreenUI lets the results rise out of the jar's mouth. A tap
    /// skips to the end.
    ///
    /// Lives on its own overlay canvas drawn above the victory window, so
    /// the jar stays at the bottom of the results.
    /// </summary>
    public class WinCelebration : MonoBehaviour
    {
        [SerializeField] private LevelController level;
        [Tooltip("HUD parts faded out when the level is won.")]
        [SerializeField] private CanvasGroup[] hud;

        [Header("Camera")]
        [SerializeField, Min(0.1f)] private float cameraTime = 1.3f;
        [Tooltip("Screen height share where the channel ends sit once the camera has dropped.")]
        [SerializeField, Range(0.4f, 0.95f)] private float outletScreenY = 0.6f;
        [Tooltip("Seconds of falling paint drawn below the outlets during the win.")]
        [SerializeField, Min(0.5f)] private float fallTime = 2.5f;

        [Header("Jar")]
        [SerializeField] private RectTransform jar;
        [Tooltip("Masked area inside the jar; paint layers are created in it.")]
        [SerializeField] private RectTransform paintArea;
        [SerializeField] private Sprite layerSprite;
        [Tooltip("Point in the middle of the jar's mouth: streams aim here, the results rise from here.")]
        [SerializeField] private RectTransform mouth;
        [SerializeField] private float mouthWidth = 190f;
        [SerializeField] private RectTransform streamParent;
        [SerializeField] private Sprite streamSprite;
        [Tooltip("Height above the jar's mouth (canvas units) where the 3D paint hands over to the UI streams.")]
        [SerializeField, Min(0f)] private float handOverHeight = 260f;
        [Tooltip("The results must stay readable: the jar shrinks to stay below this rect.")]
        [SerializeField] private RectTransform keepClear;
        [SerializeField, Range(0.3f, 1.2f)] private float maxJarScale = 0.8f;
        [SerializeField, Min(0.1f)] private float riseTime = 0.55f;
        [SerializeField, Min(0.1f)] private float fillTime = 1.5f;
        [Tooltip("Jar fill (share of the body) for 1, 2 and 3 stars.")]
        [SerializeField] private float[] fillByStars = { 0.5f, 0.72f, 0.92f };
        [Tooltip("Bottom and top of the jar body inside the sprite, as shares of its height from the bottom.")]
        [SerializeField] private Vector2 bodyRange = new Vector2(0.045f, 0.78f);

        private readonly List<Image> _layers = new List<Image>();
        private readonly List<Stream> _streams = new List<Stream>();
        private Canvas _canvas;
        private RectTransform _area;
        private Vector2 _jarRest;
        private float _jarScale = 1f;
        private bool _skip;
        private bool _jarFits = true;

        private sealed class Stream
        {
            public RouteView route;
            public Image image;
            public float mouthX;
            public float drain;   // 0 = flowing, 1 = gone into the jar
        }

        public bool IsPlaying { get; private set; }

        /// <summary>World point the results grow out of.</summary>
        public Vector3 MouthPoint => mouth != null ? mouth.position : transform.position;

        private void Awake()
        {
            _canvas = GetComponentInParent<Canvas>();
            _area = (RectTransform)transform;
            if (jar != null)
            {
                _jarRest = jar.anchoredPosition;
                jar.gameObject.SetActive(false);
            }
        }

        public IEnumerator Play(LevelResult result)
        {
            IsPlaying = true;
            _skip = false;

            StartCoroutine(FadeHud());
            if (level.Pan != null) level.Pan.SetPadVisible(false);

            // The last block finishes melting and the paint runs to the ends.
            yield return Wait(0.45f);

            // A long, steady pour into the jar: hardly any break-up into drops.
            foreach (var route in level.Routes)
                if (route.Fall != null)
                {
                    route.Fall.MaxTime = fallTime;
                    route.Fall.Breakup = 0.12f;
                    route.Fall.MinWidth = 0.6f;
                }

            PrepareJar();
            var cameraDrop = CameraDropOffset();
            var jarStarted = false;
            for (var time = 0f; time < cameraTime && !_skip; time += Time.deltaTime)
            {
                var t = time / cameraTime;
                if (level.Pan != null) level.Pan.ExtraOffset = cameraDrop * Mathf.SmoothStep(0f, 1f, t);
                if (!jarStarted && t > 0.5f)
                {
                    jarStarted = true;
                    StartCoroutine(RiseJar());
                }
                yield return null;
            }
            if (level.Pan != null) level.Pan.ExtraOffset = cameraDrop;
            if (!jarStarted) StartCoroutine(RiseJar());

            while (!_skip && jar.anchoredPosition != _jarRest) yield return null;
            jar.anchoredPosition = _jarRest;
            jar.localScale = Vector3.one * _jarScale;

            StartStreams();
            yield return Fill(result.stars);
            yield return DrainStreams();
            yield return Wobble();

            if (!_jarFits) yield return FadeJar();
            IsPlaying = false;
        }

        private void Update()
        {
            if (IsPlaying && PressedThisFrame()) _skip = true;
        }

        private void LateUpdate()
        {
            if (_streams.Count == 0) return;
            var cam = level.Camera;
            foreach (var stream in _streams)
            {
                var fall = stream.route.Fall;
                var show = fall != null && fall.IsVisible && stream.drain < 1f;
                stream.image.enabled = show;
                if (!show) continue;

                var top = ToLocal(cam.WorldToScreenPoint(fall.EndPoint));
                var bottom = (Vector2)_area.InverseTransformPoint(mouth.position) + Vector2.right * stream.mouthX;
                // Draining: the top of the stream follows the paint down into the jar.
                top = Vector2.Lerp(top, bottom, stream.drain);
                var delta = bottom - top;
                var rect = stream.image.rectTransform;
                rect.anchoredPosition = top;
                // As wide as the 3D paint where it hands over, so the pour looks unbroken.
                // (The shader's rounded, wobbling thread fills about 80% of its ribbon.)
                var width = Mathf.Max(ScreenWidthOf(cam, fall) * 0.8f, 8f) * (1f - 0.4f * stream.drain);
                rect.sizeDelta = new Vector2(width, delta.magnitude + width * 0.5f);
                // Keep the rounded ends round at any width.
                if (streamSprite != null) stream.image.pixelsPerUnitMultiplier = streamSprite.rect.width / width;
                rect.localRotation = Quaternion.FromToRotation(Vector3.down, delta);
            }
        }

        // ------------------------------------------------------------------ steps

        private IEnumerator FadeHud()
        {
            if (hud == null) yield break;
            foreach (var group in hud)
                if (group != null) group.blocksRaycasts = false;
            for (var time = 0f; time < 0.3f; time += Time.unscaledDeltaTime)
            {
                foreach (var group in hud)
                    if (group != null) group.alpha = 1f - time / 0.3f;
                yield return null;
            }
            foreach (var group in hud)
                if (group != null) group.alpha = 0f;
        }

        /// <summary>World offset that brings the channel ends up to outletScreenY.</summary>
        private Vector3 CameraDropOffset()
        {
            var cam = level.Camera;
            if (cam == null || level.Routes.Count == 0) return Vector3.zero;

            var outlet = Vector3.zero;
            foreach (var route in level.Routes)
                outlet += route.OutletPoint;
            outlet /= level.Routes.Count;

            var screenY = cam.WorldToScreenPoint(outlet).y;
            var target = outletScreenY * Screen.height;
            if (screenY >= target) return Vector3.zero;

            var depth = Vector3.Dot(outlet - cam.transform.position, cam.transform.forward);
            var perPixel = 2f * depth * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad) / Screen.height;
            return -cam.transform.up * ((target - screenY) * perPixel);
        }

        private void PrepareJar()
        {
            jar.gameObject.SetActive(true);

            // Shrink the jar to fit under the results' lowest button.
            var height = jar.rect.height;
            var room = height * maxJarScale;
            if (keepClear != null)
            {
                var corners = new Vector3[4];
                keepClear.GetWorldCorners(corners);
                var clearBottom = ToLocal(RectTransformUtility.WorldToScreenPoint(null, corners[0])).y;
                var areaBottom = _area.rect.yMin;
                room = Mathf.Min(room, clearBottom - areaBottom - 30f - _jarRest.y);
            }
            _jarScale = Mathf.Clamp(room / height, 0.4f, maxJarScale);
            _jarFits = room >= height * 0.4f;

            jar.localScale = Vector3.one * _jarScale;
            jar.anchoredPosition = _jarRest + Vector2.down * (height * _jarScale + 80f);

            foreach (var layer in _layers)
                if (layer != null) Destroy(layer.gameObject);
            _layers.Clear();
        }

        private IEnumerator RiseJar()
        {
            var start = jar.anchoredPosition;
            for (var time = 0f; time < riseTime && !_skip; time += Time.deltaTime)
            {
                var t = time / riseTime;
                jar.anchoredPosition = Vector2.LerpUnclamped(start, _jarRest, EaseOutBack(t));
                yield return null;
            }
            jar.anchoredPosition = _jarRest;
            AudioManager.PlayPop();
        }

        private void StartStreams()
        {
            var cam = level.Camera;
            var scale = _canvas != null ? _canvas.scaleFactor : 1f;
            var mouthY = RectTransformUtility.WorldToScreenPoint(null, mouth.position).y + handOverHeight * scale;

            var flowing = new List<RouteView>();
            foreach (var route in level.Routes)
                if (route.Fall != null && route.Fall.IsVisible)
                    flowing.Add(route);

            for (var index = 0; index < flowing.Count; index++)
            {
                var route = flowing[index];
                // The 3D paint stops at the jar's mouth line; the UI stream takes over.
                route.Fall.Clip = point => cam.WorldToScreenPoint(point).y < mouthY;

                var image = new GameObject("Stream", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
                image.transform.SetParent(streamParent != null ? streamParent : _area, false);
                image.sprite = streamSprite;
                image.type = Image.Type.Sliced;
                image.raycastTarget = false;
                // The 3D paint is lit; brighten the flat UI copy to match it.
                var lit = route.Fall.Color * 1.12f;
                image.color = new Color(Mathf.Clamp01(lit.r), Mathf.Clamp01(lit.g), Mathf.Clamp01(lit.b), 1f);
                image.rectTransform.pivot = new Vector2(0.5f, 1f);
                image.rectTransform.anchorMin = image.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);

                var spread = flowing.Count > 1 ? index / (flowing.Count - 1f) - 0.5f : 0f;
                _streams.Add(new Stream { route = route, image = image, mouthX = spread * mouthWidth * _jarScale });
            }
        }

        private IEnumerator Fill(int stars)
        {
            var colors = new List<Color>();
            foreach (var stream in _streams)
                colors.Add(stream.route.Fall.Color);
            if (colors.Count == 0)
                foreach (var route in level.Routes)
                    colors.Add(route.SourceColor.ToUnityColor());
            if (colors.Count == 0) yield break;

            var area = paintArea.rect.height;
            var bottom = bodyRange.x * area;
            var body = (bodyRange.y - bodyRange.x) * area;
            var fill = fillByStars[Mathf.Clamp(stars - 1, 0, fillByStars.Length - 1)] * body;
            var each = fill / colors.Count;
            var perLayer = fillTime / colors.Count;
            var waveTop = layerSprite != null ? layerSprite.border.w : 0f;

            for (var index = 0; index < colors.Count; index++)
            {
                // Each layer reaches from the jar's bottom to its own top and is
                // drawn behind the ones below it, so every boundary keeps the
                // lower layer's wavy surface.
                var layer = new GameObject("Paint layer", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
                layer.transform.SetParent(paintArea, false);
                layer.transform.SetAsFirstSibling();
                layer.sprite = layerSprite;
                layer.type = Image.Type.Sliced;
                layer.color = colors[index];
                layer.raycastTarget = false;
                var rect = layer.rectTransform;
                rect.anchorMin = new Vector2(0f, 0f);
                rect.anchorMax = new Vector2(1f, 0f);
                rect.pivot = new Vector2(0.5f, 0f);
                rect.anchoredPosition = new Vector2(0f, bottom);
                _layers.Add(layer);

                var from = each * index;
                var to = each * (index + 1);
                AudioManager.PlayPour(1f + 0.08f * index);
                for (var time = 0f; time < perLayer && !_skip; time += Time.deltaTime)
                {
                    var t = time / perLayer;
                    rect.sizeDelta = new Vector2(0f, Mathf.Lerp(from, to, Mathf.SmoothStep(0f, 1f, t)) + waveTop);
                    yield return null;
                }
                rect.sizeDelta = new Vector2(0f, to + waveTop);
            }
        }

        private IEnumerator DrainStreams()
        {
            const float duration = 0.3f;
            for (var time = 0f; time < duration && !_skip; time += Time.deltaTime)
            {
                foreach (var stream in _streams)
                    stream.drain = time / duration;
                yield return null;
            }
            foreach (var stream in _streams)
            {
                stream.drain = 1f;
                stream.image.enabled = false;
            }
        }

        private IEnumerator Wobble()
        {
            // A happy squash of the full jar.
            const float duration = 0.35f;
            for (var time = 0f; time < duration && !_skip; time += Time.unscaledDeltaTime)
            {
                var t = time / duration;
                var squash = Mathf.Sin(t * Mathf.PI * 2f) * (1f - t) * 0.08f;
                jar.localScale = new Vector3(1f + squash, 1f - squash, 1f) * _jarScale;
                yield return null;
            }
            jar.localScale = Vector3.one * _jarScale;
        }

        private IEnumerator FadeJar()
        {
            var group = jar.GetComponent<CanvasGroup>();
            if (group == null) group = jar.gameObject.AddComponent<CanvasGroup>();
            for (var time = 0f; time < 0.25f; time += Time.unscaledDeltaTime)
            {
                group.alpha = 1f - time / 0.25f;
                yield return null;
            }
            jar.gameObject.SetActive(false);
        }

        private IEnumerator Wait(float seconds)
        {
            for (var time = 0f; time < seconds && !_skip; time += Time.deltaTime)
                yield return null;
        }

        // ---------------------------------------------------------------- helpers

        /// <summary>The stream's width at its hand-over point, in canvas units.</summary>
        private float ScreenWidthOf(Camera cam, OutletFall fall)
        {
            var depth = Vector3.Dot(fall.EndPoint - cam.transform.position, cam.transform.forward);
            var worldPerPixel = 2f * depth * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad) / Screen.height;
            var scale = _canvas != null ? _canvas.scaleFactor : 1f;
            return fall.EndWidth / Mathf.Max(worldPerPixel, 1e-5f) / scale;
        }

        private Vector2 ToLocal(Vector2 screen)
        {
            var cam = _canvas != null && _canvas.renderMode != RenderMode.ScreenSpaceOverlay ? _canvas.worldCamera : null;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_area, screen, cam, out var local);
            return local;
        }

        private static float EaseOutBack(float t)
        {
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
        }

        private static bool PressedThisFrame()
        {
            var touchscreen = Touchscreen.current;
            if (touchscreen != null && touchscreen.primaryTouch.press.wasPressedThisFrame) return true;
            var mouse = Mouse.current;
            return mouse != null && mouse.leftButton.wasPressedThisFrame;
        }
    }
}
