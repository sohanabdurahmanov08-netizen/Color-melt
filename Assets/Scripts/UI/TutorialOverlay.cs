using TMPro;
using UnityEngine;

namespace ColorMelt.UI
{
    /// <summary>
    /// Tutorial visuals: a message banner and a pulsing tap marker that follows
    /// a point in the 3D scene or a HUD button. Driven by TutorialController
    /// and, for the booster tips, by BoosterBar.
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
        private RectTransform _uiTarget;
        private bool _pointing;
        private bool _swiping;
        private Rect _swipeRect;
        private float _bannerPop;

        /// <summary>True while a banner is up (the overlay hides itself when done).</summary>
        public bool IsShowing => gameObject.activeSelf;

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
            _uiTarget = null;
            _pointing = true;
            _swiping = false;
            if (pointer != null) pointer.gameObject.SetActive(true);
        }

        /// <summary>Pulses the pointer over a HUD element, e.g. a booster button.</summary>
        public void PointAtUI(RectTransform target)
        {
            _uiTarget = target;
            _pointing = target != null;
            _swiping = false;
            if (pointer != null) pointer.gameObject.SetActive(_pointing);
        }

        /// <summary>Slides the pointer back and forth across a screen rect, as a swipe gesture.</summary>
        public void SwipeAcross(Rect screenRect)
        {
            _swipeRect = screenRect;
            _swiping = true;
            _pointing = false;
            if (pointer != null) pointer.gameObject.SetActive(true);
        }

        public void HidePointer()
        {
            _pointing = false;
            _swiping = false;
            _uiTarget = null;
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

            if (_swiping && pointer != null)
            {
                // Ease from the left quarter to the right quarter and back.
                var t = Mathf.SmoothStep(0f, 1f, Mathf.PingPong(Time.unscaledTime * 0.8f, 1f));
                var swipe = new Vector2(Mathf.Lerp(_swipeRect.xMin + _swipeRect.width * 0.25f,
                    _swipeRect.xMax - _swipeRect.width * 0.25f, t), _swipeRect.center.y);
                if (RectTransformUtility.ScreenPointToLocalPointInRectangle(_area, swipe, null, out var swipeLocal))
                    pointer.anchoredPosition = swipeLocal;
                pointer.localScale = Vector3.one;
                return;
            }

            if (!_pointing || pointer == null) return;

            Vector3 screen;
            if (_uiTarget != null)
            {
                var canvas = _uiTarget.GetComponentInParent<Canvas>();
                var uiCamera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
                    ? canvas.worldCamera : null;
                screen = RectTransformUtility.WorldToScreenPoint(uiCamera, _uiTarget.TransformPoint(_uiTarget.rect.center));
            }
            else if (_camera != null)
                screen = _camera.WorldToScreenPoint(_worldPoint);
            else
                return;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(_area, screen, null, out var local))
                pointer.anchoredPosition = local;

            var pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * pulseSpeed);
            pointer.localScale = Vector3.one * Mathf.Lerp(0.85f, 1.15f, pulse);
        }
    }
}
