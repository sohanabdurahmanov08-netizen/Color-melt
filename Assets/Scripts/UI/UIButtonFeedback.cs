using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ColorMelt.UI
{
    /// <summary>
    /// Juicy press feedback for any button: squashes while held, springs back
    /// with an overshoot on release and plays the click sound. Works with
    /// Time.timeScale = 0 (paused game).
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class UIButtonFeedback : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler,
        IPointerClickHandler
    {
        [SerializeField, Range(0.5f, 1f)] private float pressedScale = 0.9f;
        [SerializeField, Min(1f)] private float stiffness = 420f;
        [SerializeField, Min(0f)] private float damping = 18f;
        [SerializeField, Min(0f)] private float releaseKick = 3.5f;
        [SerializeField] private bool clickSound = true;

        private Selectable _selectable;
        private Vector3 _baseScale;
        private float _scale = 1f;
        private float _velocity;
        private float _target = 1f;

        private void Awake()
        {
            _selectable = GetComponent<Selectable>();
            _baseScale = transform.localScale;
        }

        private void OnDisable()
        {
            _scale = _target = 1f;
            _velocity = 0f;
            transform.localScale = _baseScale;
        }

        private bool Interactable => _selectable == null || _selectable.IsInteractable();

        public void OnPointerDown(PointerEventData eventData)
        {
            if (Interactable) _target = pressedScale;
        }

        public void OnPointerUp(PointerEventData eventData) => _target = 1f;

        public void OnPointerExit(PointerEventData eventData) => _target = 1f;

        public void OnPointerClick(PointerEventData eventData)
        {
            if (!Interactable) return;
            _velocity += releaseKick;
            if (clickSound) AudioManager.PlayClick();
        }

        /// <summary>A little hop to draw attention (e.g. a reward button appearing).</summary>
        public void Punch(float strength = 4f) => _velocity += strength;

        private void Update()
        {
            var dt = Mathf.Min(Time.unscaledDeltaTime, 1f / 30f);
            var acceleration = stiffness * (_target - _scale) - damping * _velocity;
            _velocity += acceleration * dt;
            _scale += _velocity * dt;
            transform.localScale = _baseScale * _scale;
        }
    }
}
