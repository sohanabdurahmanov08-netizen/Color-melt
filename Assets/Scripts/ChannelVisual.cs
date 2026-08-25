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
        [Header("Route colour")]
        [Tooltip("Colour assigned to this slope in the level design.")]
        [SerializeField] private ColorType configuredFlowColor = ColorType.Red;
        [Tooltip("Only an editor aid: shows the configured colour before Play mode.")]
        [SerializeField, Range(0f, 1f)] private float editorPreviewFill = 1f;

        private static readonly int FillAmountId = Shader.PropertyToID("_FillAmount");
        private static readonly int FillColorId = Shader.PropertyToID("_FillColor");

        private ChannelNode _channel;
        private MaterialPropertyBlock _propertyBlock;
        private float _currentFill;
        private float _targetFill;
        // A block on this slope can cap the liquid before it reaches UV.x = 1.
        private float _fillLimit = 1f;
        private Color _targetColor = Color.white;

        public ColorType ConfiguredFlowColor => configuredFlowColor;

        private void Awake()
        {
            _channel = GetComponent<ChannelNode>();
            _propertyBlock = new MaterialPropertyBlock();

            if (targetRenderer == null)
                targetRenderer = GetComponent<Renderer>();

            // Color_rote has its mesh on a child object, while some future
            // route prefabs may keep it on the root.
            if (targetRenderer == null)
                targetRenderer = GetComponentInChildren<Renderer>();

            // Editor previews must never count as real liquid at runtime.
            ApplyVisualProperties(0f, ToUnityColor(configuredFlowColor));
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
            _targetFill = _fillLimit;
        }

        /// <summary>Assigns a design colour without changing the flow graph.</summary>
        public void SetConfiguredFlowColor(ColorType color)
        {
            configuredFlowColor = color;
            _targetColor = ToUnityColor(color);

            if (!Application.isPlaying)
                ApplyVisualProperties(editorPreviewFill, _targetColor);
        }

        /// <summary>
        /// Stops this channel's visual flow at a block. The value is UV.x along
        /// the slope: 0 is its inlet and 1 is its outlet.
        /// </summary>
        public void SetFillBarrier(float normalizedBlockStart)
        {
            _fillLimit = Mathf.Clamp01(normalizedBlockStart);

            // A flow may already have started when the level is initialized.
            if (_targetFill > _fillLimit)
                _targetFill = _fillLimit;
        }

        /// <summary>Removes the block cap and lets an existing flow finish the slope.</summary>
        public void ClearFillBarrier()
        {
            _fillLimit = 1f;
            if (_channel != null && !_channel.CurrentColor.IsEmpty())
                _targetFill = 1f;
        }

        private void Update()
        {
            if (targetRenderer == null) return;

            _currentFill = Mathf.MoveTowards(_currentFill, _targetFill, fillSpeed * Time.deltaTime);

            ApplyVisualProperties(_currentFill, _targetColor);
        }

        private void OnValidate()
        {
            if (Application.isPlaying) return;

            if (targetRenderer == null)
                targetRenderer = GetComponent<Renderer>() ?? GetComponentInChildren<Renderer>();

            if (_propertyBlock == null)
                _propertyBlock = new MaterialPropertyBlock();

            ApplyVisualProperties(editorPreviewFill, ToUnityColor(configuredFlowColor));
        }

        private void ApplyVisualProperties(float fillAmount, Color fillColor)
        {
            if (targetRenderer == null || _propertyBlock == null) return;

            targetRenderer.GetPropertyBlock(_propertyBlock);
            _propertyBlock.SetFloat(FillAmountId, fillAmount);
            _propertyBlock.SetColor(FillColorId, fillColor);
            targetRenderer.SetPropertyBlock(_propertyBlock);
        }

        /// <summary>Сопоставление игровых цветов потока с реальным Color для рендера.</summary>
        public static Color ToUnityColor(ColorType type)
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
