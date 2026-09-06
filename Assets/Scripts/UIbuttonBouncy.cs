using UnityEngine;
using UnityEngine.EventSystems;

public class UIButtonBouncy : MonoBehaviour,
    IPointerEnterHandler,
    IPointerExitHandler,
    IPointerDownHandler,
    IPointerUpHandler
{
    [Header("Hover")]
    [SerializeField] private float hoverScale = 0.95f;

    [Header("Click")]
    [SerializeField] private float clickScale = 0.88f;
    [SerializeField] private float overshootScale = 1.06f;

    [Header("Animation")]
    [SerializeField] private float animationSpeed = 14f;
    [SerializeField] private float bounceSpeed = 20f;

    private Vector3 normalScale;
    private Vector3 targetScale;

    private bool isHovering;
    private bool isPressed;

    private void Awake()
    {
        normalScale = transform.localScale;
        targetScale = normalScale;
    }

    private void Update()
    {
        float speed = isPressed ? bounceSpeed : animationSpeed;

        transform.localScale = Vector3.Lerp(
            transform.localScale,
            targetScale,
            Time.unscaledDeltaTime * speed
        );
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        isHovering = true;

        if (!isPressed)
            targetScale = normalScale * hoverScale;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        isHovering = false;

        if (!isPressed)
            targetScale = normalScale;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        isPressed = true;

        targetScale = normalScale * clickScale;
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        isPressed = false;

        StartCoroutine(BounceBack());
    }

    private System.Collections.IEnumerator BounceBack()
    {
        // Быстро увеличиваем кнопку после нажатия
        targetScale = normalScale * overshootScale;

        yield return new WaitForSecondsRealtime(0.08f);

        // Возвращаемся к размеру hover или обычному
        if (isHovering)
            targetScale = normalScale * hoverScale;
        else
            targetScale = normalScale;
    }
}
