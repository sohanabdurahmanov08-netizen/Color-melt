using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.Events;

namespace ColorMelt.UI
{
    /// <summary>
    /// Кнопка возврата в меню с анимацией нажатия (лёгкое сжатие при тапе, возврат при отпускании).
    ///
    /// Требования на сцене:
    /// - объект должен быть внутри Canvas с GraphicRaycaster;
    /// - на объекте (или на нём же) должен быть raycastable Graphic — обычно Image с
    ///   включённым Raycast Target, иначе тапы просто не долетят до этого скрипта;
    /// - в сцене должен быть EventSystem (Unity создаёт его автоматически с первым Canvas).
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class MenuReturnButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        [Header("Переход в меню")]
        [Tooltip("Имя сцены меню, добавленной в Build Settings. Если оставить пустым — " +
                 "сцена не грузится, используется только событие ниже.")]
        [SerializeField] private string menuSceneName = "Menu";

        [Tooltip("Дополнительное действие при нажатии — например, остановить музыку уровня " +
                 "или сохранить прогресс перед выходом в меню.")]
        [SerializeField] private UnityEvent onReturnToMenu;

        [Header("Анимация нажатия")]
        [SerializeField, Range(0.5f, 1f)] private float pressedScale = 0.9f;
        [SerializeField, Min(0.01f)] private float animationDuration = 0.08f;

        private RectTransform _rectTransform;
        private Vector3 _originalScale;
        private Coroutine _scaleRoutine;
        private bool _isPressed;

        private void Awake()
        {
            _rectTransform = GetComponent<RectTransform>();
            _originalScale = _rectTransform.localScale;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            _isPressed = true;
            AnimateTo(_originalScale * pressedScale);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            // Unity вызывает OnPointerUp только если палец в момент отпускания всё ещё
            // находится над этим объектом — если он соскользнул раньше, сработает
            // OnPointerExit ниже, а не этот метод. Клик засчитывается только здесь.
            if (!_isPressed) return;

            _isPressed = false;
            AnimateTo(_originalScale);
            HandleReturnToMenu();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            // Палец увели за пределы кнопки, не отпуская — отменяем нажатие без клика.
            if (!_isPressed) return;

            _isPressed = false;
            AnimateTo(_originalScale);
        }

        private void HandleReturnToMenu()
        {
            onReturnToMenu?.Invoke();

            if (!string.IsNullOrEmpty(menuSceneName))
                SceneManager.LoadScene(menuSceneName);
        }

        private void AnimateTo(Vector3 targetScale)
        {
            if (_scaleRoutine != null)
                StopCoroutine(_scaleRoutine);
            _scaleRoutine = StartCoroutine(ScaleRoutine(targetScale));
        }

        private IEnumerator ScaleRoutine(Vector3 targetScale)
        {
            var startScale = _rectTransform.localScale;
            var elapsed = 0f;

            while (elapsed < animationDuration)
            {
                // unscaledDeltaTime — чтобы кнопка анимировалась даже если игра
                // на паузе (Time.timeScale = 0), что типично для меню/паузы.
                elapsed += Time.unscaledDeltaTime;
                var t = elapsed / animationDuration;
                _rectTransform.localScale = Vector3.Lerp(startScale, targetScale, t);
                yield return null;
            }

            _rectTransform.localScale = targetScale;
        }
    }
}