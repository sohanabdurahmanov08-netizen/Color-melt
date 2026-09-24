using System;
using System.Collections;
using UnityEngine;

namespace ColorMelt.UI
{
    /// <summary>
    /// Pop-up window: fades its CanvasGroup and bounces the content in with an
    /// ease-out-back. While hidden it neither draws nor blocks taps. Use
    /// Open/Close/Toggle from code or from a button's OnClick.
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public class UIWindow : MonoBehaviour
    {
        [Tooltip("Part that scales in. The window itself (dim background) only fades.")]
        [SerializeField] private RectTransform content;
        [SerializeField, Min(0.01f)] private float openTime = 0.35f;
        [SerializeField, Min(0.01f)] private float closeTime = 0.18f;
        [SerializeField] private bool startOpen;

        private CanvasGroup _group;
        private Coroutine _animation;
        private Vector3 _contentScale = Vector3.one;

        public bool IsOpen { get; private set; }

        public event Action Opened;
        public event Action Closed;

        private void Awake()
        {
            _group = GetComponent<CanvasGroup>();
            if (content == null) content = transform as RectTransform;
            _contentScale = content.localScale;
            SetState(startOpen ? 1f : 0f, startOpen);
            IsOpen = startOpen;
        }

        public void Open()
        {
            if (IsOpen) return;
            IsOpen = true;
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            Animate(true);
            AudioManager.PlayPop();
            Opened?.Invoke();
        }

        public void Close()
        {
            if (!IsOpen) return;
            IsOpen = false;
            Animate(false);
            Closed?.Invoke();
        }

        public void Toggle()
        {
            if (IsOpen) Close();
            else Open();
        }

        private void Animate(bool open)
        {
            if (_group == null) Awake();
            if (_animation != null) StopCoroutine(_animation);
            _animation = StartCoroutine(AnimateRoutine(open));
        }

        private IEnumerator AnimateRoutine(bool open)
        {
            _group.interactable = open;
            _group.blocksRaycasts = open;

            var duration = open ? openTime : closeTime;
            var startAlpha = _group.alpha;
            for (var time = 0f; time < duration; time += Time.unscaledDeltaTime)
            {
                var t = time / duration;
                _group.alpha = Mathf.Lerp(startAlpha, open ? 1f : 0f, t);
                content.localScale = _contentScale * (open ? EaseOutBack(Mathf.Lerp(0.75f, 1f, t)) : Mathf.Lerp(1f, 0.85f, t));
                yield return null;
            }

            SetState(open ? 1f : 0f, open);
            _animation = null;
        }

        private void SetState(float alpha, bool open)
        {
            _group.alpha = alpha;
            _group.interactable = open;
            _group.blocksRaycasts = open;
            if (content != null) content.localScale = _contentScale;
        }

        private static float EaseOutBack(float x)
        {
            // Remap 0.75..1 so the overshoot happens near the end.
            var t = Mathf.InverseLerp(0.75f, 1f, x);
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            var eased = 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
            return Mathf.LerpUnclamped(0.75f, 1f, eased);
        }
    }
}
