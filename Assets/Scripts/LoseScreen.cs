using UnityEngine;

namespace ColorMelt.Core
{
    public class LoseScreen : MonoBehaviour
    {
        [SerializeField] private LevelFlowManager levelFlowManager;
        [SerializeField] private float fadeDuration = 0.3f;

        private CanvasGroup[] _canvasGroups;
        private bool _shown;

        private void Awake()
        {
            _canvasGroups = GetComponentsInChildren<CanvasGroup>(true);

            // По умолчанию экран полностью прозрачный.
            foreach (var group in _canvasGroups)
            {
                group.alpha = 0f;
                group.interactable = false;
                group.blocksRaycasts = false;
            }
        }

        private void OnEnable()
        {
            if (levelFlowManager != null)
                levelFlowManager.OnLevelLose += Show;
        }

        private void OnDisable()
        {
            if (levelFlowManager != null)
                levelFlowManager.OnLevelLose -= Show;
        }

        private void Show()
        {
            if (_shown)
                return;

            _shown = true;
            StopAllCoroutines();
            StartCoroutine(FadeIn());
        }

        private System.Collections.IEnumerator FadeIn()
        {
            float startTime = Time.unscaledTime;

            while (true)
            {
                float elapsed = Time.unscaledTime - startTime;
                float t = fadeDuration <= 0f
                    ? 1f
                    : Mathf.Clamp01(elapsed / fadeDuration);

                foreach (var group in _canvasGroups)
                {
                    group.alpha = t;
                    group.interactable = t >= 1f;
                    group.blocksRaycasts = t >= 1f;
                }

                if (t >= 1f)
                    break;

                yield return null;
            }
        }
    }
}
