using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ColorMelt.UI
{
    /// <summary>Short message pill near the bottom of the screen, e.g. "Not enough coins".</summary>
    public class Toast : MonoBehaviour
    {
        private static Toast _instance;
        private CanvasGroup _group;
        private TextMeshProUGUI _label;
        private Coroutine _routine;

        public static void Show(string message)
        {
            if (_instance == null) _instance = Create();
            if (_instance._routine != null) _instance.StopCoroutine(_instance._routine);
            _instance._routine = _instance.StartCoroutine(_instance.ShowRoutine(message));
        }

        private static Toast Create()
        {
            var root = new GameObject("Toast", typeof(Canvas), typeof(CanvasScaler));
            DontDestroyOnLoad(root);
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 900;
            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1284f, 2778f);
            scaler.matchWidthOrHeight = 0.5f;

            var pill = new GameObject("Pill", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
            pill.transform.SetParent(root.transform, false);
            var rect = (RectTransform)pill.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.22f);
            rect.sizeDelta = new Vector2(900f, 150f);
            var image = pill.GetComponent<Image>();
            image.color = new Color(0.08f, 0.06f, 0.2f, 0.92f);
            image.raycastTarget = false;

            var label = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI))
                .GetComponent<TextMeshProUGUI>();
            label.transform.SetParent(pill.transform, false);
            label.rectTransform.anchorMin = Vector2.zero;
            label.rectTransform.anchorMax = Vector2.one;
            label.rectTransform.offsetMin = new Vector2(30f, 10f);
            label.rectTransform.offsetMax = new Vector2(-30f, -10f);
            // Reuse the game's font setup; the project's default TMP material is tinted.
            var sample = FindAnyObjectByType<TextMeshProUGUI>();
            if (sample != null)
            {
                label.font = sample.font;
                label.fontSharedMaterial = sample.fontSharedMaterial;
            }
            label.alignment = TextAlignmentOptions.Center;
            label.fontSize = 54f;
            label.fontStyle = FontStyles.Bold;
            label.color = Color.white;
            label.raycastTarget = false;

            var toast = root.AddComponent<Toast>();
            toast._group = pill.GetComponent<CanvasGroup>();
            toast._group.alpha = 0f;
            toast._group.blocksRaycasts = false;
            toast._label = label;
            return toast;
        }

        private IEnumerator ShowRoutine(string message)
        {
            _label.text = message;
            var rect = (RectTransform)_group.transform;
            for (var time = 0f; time < 0.2f; time += Time.unscaledDeltaTime)
            {
                _group.alpha = time / 0.2f;
                rect.localScale = Vector3.one * Mathf.Lerp(0.8f, 1f, time / 0.2f);
                yield return null;
            }
            _group.alpha = 1f;
            rect.localScale = Vector3.one;
            yield return new WaitForSecondsRealtime(1.6f);
            for (var time = 0f; time < 0.25f; time += Time.unscaledDeltaTime)
            {
                _group.alpha = 1f - time / 0.25f;
                yield return null;
            }
            _group.alpha = 0f;
            _routine = null;
        }
    }
}
