using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace ColorMelt.Gameplay
{
    /// <summary>
    /// A pad at the bottom of the screen: dragging in it slides the camera
    /// left and right over the board, and dragging up brings it a little
    /// closer to the channels. Taps on the pad never reach the board.
    ///
    /// The offset is applied on top of the pose LevelController frames for
    /// the level, so a restart always starts from the whole board in view.
    /// </summary>
    public class CameraPan : MonoBehaviour
    {
        private static readonly Color PadColor = new Color(1f, 1f, 1f, 0.45f);
        private static readonly Color ArrowColor = new Color(0x2B / 255f, 0x23 / 255f, 0x50 / 255f, 0.55f);

        [SerializeField] private LevelController level;

        [Header("Pad (viewport fractions)")]
        [SerializeField] private Rect zone = new Rect(0.14f, 0.125f, 0.72f, 0.07f);
        [Tooltip("Gap kept between the pad and the board, as a fraction of screen height.")]
        [SerializeField, Range(0f, 0.1f)] private float boardGap = 0.015f;

        [Header("Movement")]
        [Tooltip("How much closer the camera may come, as a share of its framed distance.")]
        [SerializeField, Range(0f, 0.8f)] private float maxCloser = 0.35f;
        [Tooltip("Sideways range without zoom, as a share of half the board width. Grows to the full half width at max zoom.")]
        [SerializeField, Range(0f, 1f)] private float sideRangeAtRest = 0.35f;
        [SerializeField, Min(0f)] private float closerSpeed = 1.5f;
        [SerializeField, Min(0f)] private float smoothing = 14f;

        private readonly List<RaycastResult> _uiHits = new List<RaycastResult>();
        private RectTransform _pad;
        private Image _padImage;
        private bool _highlighted;
        private bool _dragging;
        private Vector2 _lastPointer;
        private Vector2 _target;   // x: sideways, y: closer (world units)
        private Vector2 _current;

        /// <summary>Total distance dragged on the pad, in screen pixels.</summary>
        public float DragDistance { get; private set; }

        /// <summary>Screen height share the board must leave free for the pad.</summary>
        public float ReservedBottom => isActiveAndEnabled ? zone.yMax + boardGap : 0f;

        public Rect ScreenRect => new Rect(zone.x * Screen.width, zone.y * Screen.height,
            zone.width * Screen.width, zone.height * Screen.height);

        public bool Contains(Vector2 screenPosition) => isActiveAndEnabled && ScreenRect.Contains(screenPosition);

        /// <summary>Makes the pad pulse, e.g. while the tutorial points at it.</summary>
        public void SetHighlighted(bool highlighted) => _highlighted = highlighted;

        private void Awake()
        {
            if (level == null) level = GetComponent<LevelController>();
            BuildPad();
        }

        private void OnEnable()
        {
            if (_pad != null) _pad.gameObject.SetActive(true);
        }

        private void OnDisable()
        {
            if (_pad != null) _pad.gameObject.SetActive(false);
        }

        private void Update()
        {
            HandleInput();
            AnimatePad();
        }

        private void LateUpdate()
        {
            var cam = level != null ? level.Camera : null;
            if (cam == null || !level.IsFramed) return;

            _current = Vector2.Lerp(_current, _target, 1f - Mathf.Exp(-smoothing * Time.unscaledDeltaTime));
            var t = cam.transform;
            t.position = level.FramedPosition + t.right * _current.x + t.forward * _current.y;
        }

        private void HandleInput()
        {
            if (level == null || !level.IsFramed) return;

            var pressed = TryGetPointer(out var pointer, out var pressedThisFrame);
            if (!pressed)
            {
                _dragging = false;
                return;
            }

            if (pressedThisFrame)
            {
                _dragging = Contains(pointer) && !IsOverWindow(pointer);
                _lastPointer = pointer;
                return;
            }

            if (!_dragging) return;

            var delta = pointer - _lastPointer;
            _lastPointer = pointer;
            if (delta == Vector2.zero) return;
            DragDistance += delta.magnitude;

            // World units per pixel at the board's depth, so the board follows the finger.
            var cam = level.Camera;
            var depth = Mathf.Max(0.01f, BoardDepth(cam) - _target.y);
            var perPixel = 2f * depth * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad) / Screen.height;

            var maxForward = BoardDepth(cam) * maxCloser;
            _target.y = Mathf.Clamp(_target.y + delta.y * perPixel * closerSpeed, 0f, maxForward);

            var zoom = maxForward > 0f ? _target.y / maxForward : 0f;
            var halfWidth = BoardHalfWidth(cam);
            var side = halfWidth * Mathf.Lerp(sideRangeAtRest, 1f, zoom);
            _target.x = Mathf.Clamp(_target.x - delta.x * perPixel, -side, side);
        }

        /// <summary>Distance from the framed pose to the board centre along the view.</summary>
        private float BoardDepth(Camera cam) =>
            Vector3.Dot(level.BoardBounds.center - level.FramedPosition, cam.transform.forward);

        private float BoardHalfWidth(Camera cam)
        {
            var e = level.BoardBounds.extents;
            var r = cam.transform.right;
            return Mathf.Abs(r.x) * e.x + Mathf.Abs(r.y) * e.y + Mathf.Abs(r.z) * e.z;
        }

        // Open windows (win/lose/pause) and buttons keep the pad inactive.
        private bool IsOverWindow(Vector2 screenPosition)
        {
            var eventSystem = EventSystem.current;
            if (eventSystem == null) return false;

            _uiHits.Clear();
            eventSystem.RaycastAll(new PointerEventData(eventSystem) { position = screenPosition }, _uiHits);
            foreach (var hit in _uiHits)
            {
                var selectable = hit.gameObject.GetComponentInParent<Selectable>();
                if (selectable != null && selectable.IsInteractable() && selectable.isActiveAndEnabled)
                    return true;
                var group = hit.gameObject.GetComponentInParent<CanvasGroup>();
                if (group != null && group.blocksRaycasts && group.alpha > 0.01f)
                    return true;
            }
            return false;
        }

        private static bool TryGetPointer(out Vector2 position, out bool pressedThisFrame)
        {
            var touchscreen = Touchscreen.current;
            if (touchscreen != null && touchscreen.primaryTouch.press.isPressed)
            {
                position = touchscreen.primaryTouch.position.ReadValue();
                pressedThisFrame = touchscreen.primaryTouch.press.wasPressedThisFrame;
                return true;
            }

            var mouse = Mouse.current;
            if (mouse != null && mouse.leftButton.isPressed)
            {
                position = mouse.position.ReadValue();
                pressedThisFrame = mouse.leftButton.wasPressedThisFrame;
                return true;
            }

            position = default;
            pressedThisFrame = false;
            return false;
        }

        private void AnimatePad()
        {
            if (_pad == null) return;

            var pulse = _highlighted ? 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 5f) : 0f;
            var alpha = _dragging ? 0.75f : Mathf.Lerp(PadColor.a, 0.9f, pulse);
            _padImage.color = new Color(PadColor.r, PadColor.g, PadColor.b, alpha);
            _pad.localScale = Vector3.one * (1f + 0.05f * pulse);
        }

        private void BuildPad()
        {
            var canvasObject = new GameObject("Camera Pan Pad", typeof(Canvas), typeof(CanvasScaler));
            canvasObject.transform.SetParent(transform, false);
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            // Behind the HUD and the windows.
            canvas.sortingOrder = -10;
            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1284f, 2778f);
            scaler.matchWidthOrHeight = 0.5f;

            _pad = new GameObject("Pad", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
            _pad.SetParent(canvasObject.transform, false);
            _pad.anchorMin = zone.min;
            _pad.anchorMax = zone.max;
            _pad.offsetMin = _pad.offsetMax = Vector2.zero;

            _padImage = _pad.GetComponent<Image>();
            _padImage.sprite = Resources.Load<Sprite>("UI/PanPad");
            _padImage.type = Image.Type.Sliced;
            _padImage.color = PadColor;
            _padImage.raycastTarget = false;

            var arrow = Resources.Load<Sprite>("UI/PanArrow");
            AddArrow(arrow, 0.12f, 0f);    // left
            AddArrow(arrow, 0.5f, -90f);   // up: closer
            AddArrow(arrow, 0.88f, 180f);  // right
        }

        private void AddArrow(Sprite sprite, float x, float angle)
        {
            var rect = new GameObject("Arrow", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
            rect.SetParent(_pad, false);
            rect.anchorMin = rect.anchorMax = new Vector2(x, 0.5f);
            rect.sizeDelta = new Vector2(84f, 84f);
            rect.localRotation = Quaternion.Euler(0f, 0f, angle);

            var image = rect.GetComponent<Image>();
            image.sprite = sprite;
            image.preserveAspect = true;
            image.color = ArrowColor;
            image.raycastTarget = false;
        }
    }
}
