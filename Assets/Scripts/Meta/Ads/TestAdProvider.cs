using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ColorMelt.Meta
{
    /// <summary>
    /// Simulated test ad for targets where LevelPlay does not run (the Editor
    /// with a PC target, desktop builds). It behaves like a real one: a
    /// full-screen card with a countdown; a rewarded ad pays out only if the
    /// player waits for the countdown, an interstitial can be closed once it
    /// ends. The UI is built in code so no scene needs editing.
    /// </summary>
    internal class TestAdProvider : MonoBehaviour, IAdProvider
    {
        // Fully opaque: in linear colour space even a few percent of
        // transparency lets the game show through.
        private static readonly Color Backdrop = new Color32(0x1E, 0x18, 0x38, 0xFF);
        private static readonly Color Ink = new Color32(0x2B, 0x23, 0x50, 0xFF);
        private static readonly Color Yellow = new Color32(0xFF, 0xC9, 0x3C, 0xFF);
        private static readonly Color Green = new Color32(0x4C, 0xC9, 0x6B, 0xFF);
        private static readonly Color Lavender = new Color32(0xC9, 0xC1, 0xF0, 0xFF);
        private static readonly Color[] DropColours =
        {
            new Color32(0xF2, 0x4B, 0x5B, 0xFF),
            new Color32(0xFF, 0xC9, 0x3C, 0xFF),
            new Color32(0x3D, 0x8B, 0xF2, 0xFF)
        };

        // Corner radius of the 64 px rounded sprite; its 9-slice border adds a 1 px margin.
        private const float RoundedRadius = 30f;
        private const float RoundedBorder = RoundedRadius + 1f;

        private Canvas _canvas;
        private CanvasGroup _group;
        private RectTransform _card;
        private TextMeshProUGUI _title;
        private TextMeshProUGUI _subtitle;
        private TextMeshProUGUI _status;
        private RectTransform _barFill;
        private Image _button;
        private TextMeshProUGUI _buttonLabel;
        private Sprite _rounded;
        private Sprite _circle;
        private bool _open;
        private bool _pressed;

        public bool IsRewardedReady => !_open;
        public bool IsInterstitialReady => !_open;

        public void ShowRewarded(string placement, Action onRewarded, Action onClosed) =>
            StartCoroutine(Play(true, placement, onRewarded, onClosed));

        public void ShowInterstitial(string placement, Action onClosed) =>
            StartCoroutine(Play(false, placement, null, onClosed));

        private IEnumerator Play(bool rewarded, string placement, Action onRewarded, Action onClosed)
        {
            _open = true;
            if (_canvas == null) Build();
            Debug.Log($"Ads: showing test {(rewarded ? "rewarded" : "interstitial")} '{placement}'.");

            var duration = AdsConfig.Instance.testAdSeconds;
            _title.text = Localization.Get("ads.test_title");
            _subtitle.text = $"{Localization.Get(rewarded ? "ads.kind.rewarded" : "ads.kind.interstitial")}\n" +
                             $"<size=70%>{placement}</size>";
            SetButton(rewarded, Localization.Get("ads.skip"), Color.white);
            _pressed = false;
            _canvas.enabled = true;

            // Pop in.
            for (var time = 0f; time < 0.2f; time += Time.unscaledDeltaTime)
            {
                var t = time / 0.2f;
                _group.alpha = t;
                _card.localScale = Vector3.one * Mathf.Lerp(0.85f, 1f, 1f - (1f - t) * (1f - t));
                yield return null;
            }
            _group.alpha = 1f;
            _card.localScale = Vector3.one;

            // Countdown. A rewarded ad can be skipped, but then pays nothing.
            var finished = false;
            for (var time = 0f; ; time += Time.unscaledDeltaTime)
            {
                if (_pressed) break;
                if (time >= duration)
                {
                    finished = true;
                    break;
                }
                SetProgress(time / duration);
                var left = Mathf.CeilToInt(duration - time);
                _status.text = Localization.Format(rewarded ? "ads.reward_in" : "ads.close_in", left);
                yield return null;
            }

            if (finished)
            {
                SetProgress(1f);
                if (rewarded)
                {
                    _status.text = Localization.Get("ads.reward_earned");
                    onRewarded?.Invoke();
                    SetButton(true, Localization.Get("ads.close"), Green);
                }
                else
                {
                    _status.text = string.Empty;
                    SetButton(true, Localization.Get("ads.close"), Color.white);
                }

                _pressed = false;
                while (!_pressed) yield return null;
            }

            // Fade out.
            for (var time = 0f; time < 0.15f; time += Time.unscaledDeltaTime)
            {
                _group.alpha = 1f - time / 0.15f;
                yield return null;
            }
            _canvas.enabled = false;
            _open = false;
            Debug.Log($"Ads: test {(rewarded ? "rewarded" : "interstitial")} closed{(rewarded && !finished ? " early, no reward" : "")}.");
            onClosed?.Invoke();
        }

        private void SetProgress(float progress) => _barFill.anchorMax = new Vector2(Mathf.Clamp01(progress), 1f);

        private void SetButton(bool visible, string label, Color colour)
        {
            _button.gameObject.SetActive(visible);
            _button.color = colour;
            _buttonLabel.text = label;
            _buttonLabel.color = colour == Color.white ? Ink : Color.white;
        }

        // ---- UI built in code ----

        private void Build()
        {
            _rounded = CreateDiscSprite(64, RoundedRadius);
            _circle = CreateDiscSprite(128, 63f);

            var root = new GameObject("Test Ad", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(CanvasGroup));
            root.transform.SetParent(transform, false);
            _canvas = root.GetComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            // Above the scene transition (1000) and toasts (900).
            _canvas.sortingOrder = 2000;
            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1284f, 2778f);
            scaler.matchWidthOrHeight = 0.5f;
            _group = root.GetComponent<CanvasGroup>();

            // Full-screen backdrop that also swallows every tap behind the ad.
            var backdrop = CreateImage("Backdrop", root.transform, Backdrop, false);
            backdrop.raycastTarget = true;
            Stretch(backdrop.rectTransform);

            var badge = CreateImage("Badge", root.transform, Yellow, true, 40f);
            badge.rectTransform.anchorMin = badge.rectTransform.anchorMax = new Vector2(0f, 1f);
            badge.rectTransform.pivot = new Vector2(0f, 1f);
            badge.rectTransform.anchoredPosition = new Vector2(60f, -170f);
            badge.rectTransform.sizeDelta = new Vector2(150f, 80f);
            var badgeLabel = CreateText("Label", badge.transform, 44f, Ink);
            Stretch(badgeLabel.rectTransform);
            badgeLabel.text = "AD";

            _card = new GameObject("Card", typeof(RectTransform)).GetComponent<RectTransform>();
            _card.SetParent(root.transform, false);
            _card.sizeDelta = new Vector2(1100f, 1500f);

            // Three overlapping paint drops, the game's primaries.
            for (var index = 0; index < DropColours.Length; index++)
            {
                var drop = CreateImage("Drop", _card, DropColours[index], false);
                drop.sprite = _circle;
                drop.rectTransform.sizeDelta = new Vector2(260f, 260f);
                drop.rectTransform.anchoredPosition = new Vector2((index - 1) * 170f, 470f + (index == 1 ? 60f : 0f));
            }

            _title = CreateText("Title", _card, 110f, Color.white);
            _title.textWrappingMode = TextWrappingModes.NoWrap;
            _title.enableAutoSizing = true;
            _title.fontSizeMin = 60f;
            _title.fontSizeMax = 110f;
            Place(_title.rectTransform, new Vector2(0f, 170f), new Vector2(1000f, 160f));

            _subtitle = CreateText("Subtitle", _card, 62f, Lavender);
            Place(_subtitle.rectTransform, new Vector2(0f, -10f), new Vector2(1100f, 160f));

            var bar = CreateImage("Bar", _card, new Color(1f, 1f, 1f, 0.15f), true, 24f);
            Place(bar.rectTransform, new Vector2(0f, -190f), new Vector2(900f, 48f));
            var fill = CreateImage("Fill", bar.transform, Yellow, true, 24f);
            _barFill = fill.rectTransform;
            _barFill.anchorMin = Vector2.zero;
            _barFill.anchorMax = new Vector2(0f, 1f);
            _barFill.offsetMin = _barFill.offsetMax = Vector2.zero;

            _status = CreateText("Status", _card, 60f, Color.white);
            Place(_status.rectTransform, new Vector2(0f, -300f), new Vector2(1100f, 110f));

            _button = CreateImage("Button", _card, Color.white, true, 85f);
            _button.raycastTarget = true;
            Place(_button.rectTransform, new Vector2(0f, -560f), new Vector2(860f, 170f));
            _button.gameObject.AddComponent<Button>().onClick.AddListener(() => _pressed = true);
            _buttonLabel = CreateText("Label", _button.transform, 58f, Ink);
            Stretch(_buttonLabel.rectTransform);

            _canvas.enabled = false;
        }

        private Image CreateImage(string name, Transform parent, Color colour, bool rounded, float cornerRadius = 0f)
        {
            var image = new GameObject(name, typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            image.transform.SetParent(parent, false);
            image.color = colour;
            image.raycastTarget = false;
            if (rounded)
            {
                image.sprite = _rounded;
                image.type = Image.Type.Sliced;
                image.pixelsPerUnitMultiplier = RoundedBorder / cornerRadius;
            }
            return image;
        }

        private static TextMeshProUGUI CreateText(string name, Transform parent, float size, Color colour)
        {
            // New TMP text takes the project's default font (Nunito ExtraBold).
            var text = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
            text.transform.SetParent(parent, false);
            text.fontSize = size;
            text.color = colour;
            text.alignment = TextAlignmentOptions.Center;
            text.raycastTarget = false;
            return text;
        }

        private static void Place(RectTransform rect, Vector2 position, Vector2 size)
        {
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        /// <summary>
        /// White anti-aliased rounded square with a 1 px clear margin (so the
        /// edge is never cut off); with a radius below half the size it is
        /// 9-sliced and draws rounded rectangles of any size.
        /// </summary>
        private static Sprite CreateDiscSprite(int size, float radius)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
            var pixels = new Color32[size * size];
            var half = size / 2f;
            var inner = half - 1f - radius;
            for (var y = 0; y < size; y++)
            for (var x = 0; x < size; x++)
            {
                var dx = Mathf.Max(0f, Mathf.Abs(x + 0.5f - half) - inner);
                var dy = Mathf.Max(0f, Mathf.Abs(y + 0.5f - half) - inner);
                var alpha = Mathf.Clamp01(radius - Mathf.Sqrt(dx * dx + dy * dy) + 0.5f);
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)(alpha * 255f));
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            var slice = radius + 1f;
            var border = slice < half ? new Vector4(slice, slice, slice, slice) : Vector4.zero;
            return Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, border);
        }
    }
}
