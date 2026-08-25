using UnityEngine;

namespace ColorMelt.Core
{
    /// <summary>
    /// Вешается на тот же объект, что и ChannelNode.
    /// Подписывается на OnFlowChanged и плавно анимирует материал шейдера ChannelFill:
    /// _FillAmount (0 → 1 при заполнении, 1 → 0 при сбросе) и _FillColor (цвет потока).
    ///
    /// Требования к модели канала:
    /// - Renderer с материалом на шейдере "ColorMelt/ChannelFill"
    /// - UV.x размечен вдоль длины канала: 0 у входа, 1 у выхода
    /// </summary>
    [RequireComponent(typeof(ChannelNode))]
    public class ChannelVisual : MonoBehaviour
    {
        [SerializeField] private Renderer targetRenderer;
        [SerializeField] private float fillSpeed = 2f; // единиц fillAmount в секунду

        private static readonly int FillAmountId = Shader.PropertyToID("_FillAmount");
        private static readonly int FillColorId = Shader.PropertyToID("_FillColor");

        private ChannelNode _channel;
        private MaterialPropertyBlock _propertyBlock;
        private float _currentFill;
        private float _targetFill;
        private Color _targetColor = Color.white;

        private void Awake()
        {
            _channel = GetComponent<ChannelNode>();
            _propertyBlock = new MaterialPropertyBlock();

            if (targetRenderer == null)
                targetRenderer = GetComponent<Renderer>();
        }

        private void OnEnable() => _channel.OnFlowChanged += HandleFlowChanged;

        private void OnDisable() => _channel.OnFlowChanged -= HandleFlowChanged;

        private void HandleFlowChanged(ColorType newColor)
        {
            if (newColor.IsEmpty())
            {
                _targetFill = 0f;
                return;
            }

            _targetColor = ToUnityColor(newColor);
            _targetFill = 1f;
        }

        private void Update()
        {
            if (targetRenderer == null) return;

            _currentFill = Mathf.MoveTowards(_currentFill, _targetFill, fillSpeed * Time.deltaTime);

            targetRenderer.GetPropertyBlock(_propertyBlock);
            _propertyBlock.SetFloat(FillAmountId, _currentFill);
            _propertyBlock.SetColor(FillColorId, _targetColor);
            targetRenderer.SetPropertyBlock(_propertyBlock);
        }

        /// <summary>Сопоставление игровых цветов потока с реальным Color для рендера.</summary>
        private static Color ToUnityColor(ColorType type)
        {
            switch (type)
            {
                case ColorType.Red:    return new Color(0.90f, 0.15f, 0.15f);
                case ColorType.Blue:   return new Color(0.15f, 0.35f, 0.95f);
                case ColorType.Yellow: return new Color(0.98f, 0.85f, 0.10f);
                case ColorType.Purple: return new Color(0.55f, 0.15f, 0.75f);
                case ColorType.Green:  return new Color(0.15f, 0.75f, 0.30f);
                case ColorType.Orange: return new Color(0.95f, 0.50f, 0.10f);
                case ColorType.Brown:  return new Color(0.45f, 0.30f, 0.15f);
                default:               return Color.white;
            }
        }
    }
}
