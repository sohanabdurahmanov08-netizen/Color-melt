using TMPro;
using UnityEngine;

namespace ColorMelt.UI
{
    /// <summary>
    /// Tutorial visuals: a message banner and a pulsing tap marker that follows
    /// a point in the 3D scene. Driven by TutorialController.
    /// </summary>
    public class TutorialOverlay : MonoBehaviour
    {
        [SerializeField] private CanvasGroup banner;
        [SerializeField] private TMP_Text message;
        [SerializeField] private RectTransform pointer;
        [SerializeField, Min(0f)] private float pulseSpeed = 5f;
        [Tooltip("HUD parts the banner covers; hidden while the tutorial is shown.")]
        [SerializeField] private GameObject[] hideWhileShown;

        private RectTransform _area;
        private Camera _camera;
        private Vector3 _worldPoint;
        private bool _pointing;
        private float _bannerPop;

        private void Awake()
        {
            _area = (RectTransform)transform;
            if (banner != null) banner.alpha = 0f;
            if (pointer != null) pointer.gameObject.SetActive(false);
        }

        public void ShowMessage(string text)
        {
            gameObject.SetActive(true);
            SetCoveredVisible(false);
            if (message != null) message.text = text;
            _bannerPop = 1f;
        }

        public void PointAt(Vector3 worldPoint, Camera cam)
        {
            _worldPoint = worldPoint;
            _camera = cam;
            _pointing = true;
            if (pointer != null) pointer.gameObject.SetActive(true);
        }

        public void HidePointer()
        {
            _pointing = false;
            if (pointer != null) pointer.gameObject.SetActive(false);
        }

        public void Hide()
        {
            HidePointer();
            if (banner != null) banner.alpha = 0f;
            SetCoveredVisible(true);
            gameObject.SetActive(false);
        }

        private void SetCoveredVisible(bool visible)
        {
            if (hideWhileShown == null) return;
            foreach (var covered in hideWhileShown)
                if (covered != null) covered.SetActive(visible);
        }

        private void Update()
        {
            if (banner != null)
            {
                banner.alpha = Mathf.MoveTowards(banner.alpha, 1f, Time.unscaledDeltaTime * 5f);
                _bannerPop = Mathf.MoveTowards(_bannerPop, 0f, Time.unscaledDeltaTime * 4f);
                banner.transform.localScale = Vector3.one * (1f + 0.08f * Mathf.Sin(_bannerPop * Mathf.PI));
            }

            if (!_pointing || pointer == null || _camera == null) return;

            var screen = _camera.WorldToScreenPoint(_worldPoint);
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(_area, screen, null, out var local))
                pointer.anchoredPosition = local;

            var pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * pulseSpeed);
            pointer.localScale = Vector3.one * Mathf.Lerp(0.85f, 1.15f, pulse);
        }
    }
}
