using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ColorMelt.Gameplay
{
    /// <summary>
    /// Scene change as a paint pour: paint of a random colour runs down the
    /// screen in drips until it covers everything, the next scene loads
    /// underneath, then the paint drains off the bottom and reveals it.
    /// Drawn by the ColorMelt/PaintTransition shader through
    /// Resources/PaintTransition; without it the screen simply fades.
    /// </summary>
    public class SceneFader : MonoBehaviour
    {
        private const string MaterialResource = "PaintTransition";
        private const float CoverTime = 0.6f;
        private const float RevealTime = 0.7f;

        // Paint edges run past the bottom far enough that drips and trailing
        // streaks (up to ~0.5 screen widths) leave the screen too.
        private const float Overshoot = 0.12f;
        private const float StreakLength = 0.5f;

        private static readonly int FrontId = Shader.PropertyToID("_Front");
        private static readonly int BackId = Shader.PropertyToID("_Back");
        private static readonly int GrowId = Shader.PropertyToID("_Grow");
        private static readonly int AspectId = Shader.PropertyToID("_Aspect");
        private static readonly int SeedId = Shader.PropertyToID("_Seed");

        private static SceneFader _instance;
        private Image _paint;
        private Material _material;
        private float _lastHue = -1f;
        private bool _busy;

        public static SceneFader Instance
        {
            get
            {
                if (_instance == null)
                    _instance = Create();
                return _instance;
            }
        }

        private static SceneFader Create()
        {
            var root = new GameObject("Scene Transition", typeof(Canvas), typeof(GraphicRaycaster));
            DontDestroyOnLoad(root);
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 1000;

            var paint = new GameObject("Paint", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            paint.transform.SetParent(root.transform, false);
            var rect = paint.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            paint.enabled = false;

            var fader = root.AddComponent<SceneFader>();
            fader._paint = paint;
            var source = Resources.Load<Material>(MaterialResource);
            if (source != null)
            {
                fader._material = new Material(source);
                paint.material = fader._material;
            }
            return fader;
        }

        public void FadeTo(string scene)
        {
            if (_busy) return;
            StartCoroutine(_material != null ? PourRoutine(scene) : FadeRoutine(scene));
        }

        private IEnumerator PourRoutine(string scene)
        {
            _busy = true;
            var aspect = (float)Screen.height / Mathf.Max(1, Screen.width);
            var covered = aspect + Overshoot;
            _material.SetFloat(AspectId, aspect);
            _material.SetFloat(SeedId, Random.Range(0f, 100f));
            _paint.color = NextColour();
            SetEdges(-Overshoot, -10f, 0f);
            _paint.enabled = true;

            // Pour: drips shoot ahead, then the paint front follows them down.
            for (var time = 0f; time < CoverTime; time += Time.unscaledDeltaTime)
            {
                var t = time / CoverTime;
                SetEdges(Mathf.Lerp(-Overshoot, covered, t * t * (3f - 2f * t)), -10f, Mathf.Clamp01(t * 2f));
                yield return null;
            }
            SetEdges(covered, -10f, 1f);

            yield return SceneManager.LoadSceneAsync(scene);
            // Give the new scene a frame to build before revealing it.
            yield return null;

            // Drain: the top of the paint slides down, trailing streaks.
            for (var time = 0f; time < RevealTime; time += Time.unscaledDeltaTime)
            {
                var t = time / RevealTime;
                var eased = t * t;
                SetEdges(covered + eased, Mathf.Lerp(-Overshoot, covered + StreakLength, eased), 1f);
                yield return null;
            }

            _paint.enabled = false;
            _busy = false;
        }

        private void SetEdges(float front, float back, float grow)
        {
            _material.SetFloat(FrontId, front);
            _material.SetFloat(BackId, back);
            _material.SetFloat(GrowId, grow);
        }

        /// <summary>Bright random paint, never too close in hue to the last one.</summary>
        private Color NextColour()
        {
            var hue = Random.value;
            for (var attempt = 0; attempt < 8 && _lastHue >= 0f && Mathf.Abs(Mathf.DeltaAngle(hue * 360f, _lastHue * 360f)) < 50f; attempt++)
                hue = Random.value;
            _lastHue = hue;
            return Color.HSVToRGB(hue, Random.Range(0.6f, 0.85f), Random.Range(0.85f, 1f));
        }

        private IEnumerator FadeRoutine(string scene)
        {
            _busy = true;
            _paint.enabled = true;
            var colour = new Color(0.06f, 0.05f, 0.14f, 0f);
            for (var time = 0f; time < 0.25f; time += Time.unscaledDeltaTime)
            {
                colour.a = time / 0.25f;
                _paint.color = colour;
                yield return null;
            }
            colour.a = 1f;
            _paint.color = colour;
            yield return SceneManager.LoadSceneAsync(scene);
            for (var time = 0f; time < 0.25f; time += Time.unscaledDeltaTime)
            {
                colour.a = 1f - time / 0.25f;
                _paint.color = colour;
                yield return null;
            }
            _paint.enabled = false;
            _busy = false;
        }
    }
}
