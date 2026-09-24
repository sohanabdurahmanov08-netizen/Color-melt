using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ColorMelt.Gameplay
{
    /// <summary>Full-screen fade used for every scene change.</summary>
    public class SceneFader : MonoBehaviour
    {
        private const float FadeTime = 0.25f;

        private static SceneFader _instance;
        private CanvasGroup _group;
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
            var root = new GameObject("Scene Fader", typeof(Canvas), typeof(CanvasGroup));
            DontDestroyOnLoad(root);
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 1000;

            var image = new GameObject("Fade", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            image.transform.SetParent(root.transform, false);
            image.color = new Color(0.06f, 0.05f, 0.14f, 1f);
            var rect = image.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;

            var fader = root.AddComponent<SceneFader>();
            fader._group = root.GetComponent<CanvasGroup>();
            fader._group.alpha = 0f;
            fader._group.blocksRaycasts = false;
            return fader;
        }

        public void FadeTo(string scene)
        {
            if (_busy) return;
            StartCoroutine(FadeRoutine(scene));
        }

        private IEnumerator FadeRoutine(string scene)
        {
            _busy = true;
            _group.blocksRaycasts = true;
            yield return Fade(0f, 1f);
            yield return SceneManager.LoadSceneAsync(scene);
            yield return Fade(1f, 0f);
            _group.blocksRaycasts = false;
            _busy = false;
        }

        private IEnumerator Fade(float from, float to)
        {
            for (var time = 0f; time < FadeTime; time += Time.unscaledDeltaTime)
            {
                _group.alpha = Mathf.Lerp(from, to, time / FadeTime);
                yield return null;
            }
            _group.alpha = to;
        }
    }
}
