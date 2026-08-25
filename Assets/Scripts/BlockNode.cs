using System.Collections.Generic;
using UnityEngine;

namespace ColorMelt.Core
{
    /// <summary>
    /// Цветной блок-преграда. Разрушается, когда получает поток точно нужного цвета.
    /// В MVP блок — конечная точка ветки графа; открытие прохода за блоком
    /// после разрушения добавим отдельно, когда появится вторая цепочка уровня.
    /// </summary>
    public class BlockNode : MonoBehaviour, IFlowNode
    {
        [SerializeField] private ColorType requiredColor;
        [Header("Flow barrier visual")]
        [Tooltip("The ChannelVisual on the slope immediately before this block.")]
        [SerializeField] private ChannelVisual blockedChannel;
        [Tooltip("Block start along that slope's UV.x. 0 = inlet, 1 = outlet.")]
        [SerializeField, Range(0f, 1f)] private float blockStartNormalized = 0.8f;
        [Tooltip("Optional visible block object to hide when the block is melted.")]
        [SerializeField] private GameObject blockVisual;
        [Header("Block colour preview")]
        [Tooltip("Optional mesh renderer used to show the block's required colour.")]
        [SerializeField] private Renderer colorRenderer;
        [Tooltip("When enabled, the visual block colour follows Required Color.")]
        [SerializeField] private bool useRequiredColorForVisual = true;
        [SerializeField] private ColorType visualColor = ColorType.Red;
        [Tooltip("Optional node reached after this block has been destroyed.")]
        [SerializeField] private FlowNodeRef output;

        private IFlowNode _runtimeOutput;

        public bool IsDestroyed { get; private set; }
        public ColorType CurrentColor { get; private set; } = ColorType.None;
        public ColorType RequiredColor => requiredColor;
        public float BlockStartNormalized => blockStartNormalized;

        /// <summary>Вызывается один раз, в момент разрушения — на него подписывается LevelFlowManager.</summary>
        public System.Action OnDestroyedByFlow;

        private void Awake()
        {
            if (!IsDestroyed)
                blockedChannel?.SetFillBarrier(blockStartNormalized);

            ApplyVisualColor();
        }

        private void OnValidate() => ApplyVisualColor();

        /// <summary>Configures a block that was created by the level generator.</summary>
        public void Configure(ColorType required, ChannelVisual channelBeforeBlock,
            float normalizedBlockStart, GameObject visualToHide, IFlowNode nextNode = null)
        {
            requiredColor = required;
            blockedChannel = channelBeforeBlock;
            blockStartNormalized = Mathf.Clamp01(normalizedBlockStart);
            blockVisual = visualToHide;
            _runtimeOutput = nextNode;
            blockedChannel?.SetFillBarrier(blockStartNormalized);
            if (colorRenderer == null && visualToHide != null)
                colorRenderer = visualToHide.GetComponentInChildren<Renderer>();
            visualColor = required;
            useRequiredColorForVisual = true;
            ApplyVisualColor();
        }

        /// <summary>Lets a designer choose a different display colour if needed.</summary>
        public void SetVisualColor(ColorType color)
        {
            visualColor = color;
            useRequiredColorForVisual = false;
            ApplyVisualColor();
        }

        private void ApplyVisualColor()
        {
            if (colorRenderer == null)
                colorRenderer = GetComponentInChildren<Renderer>();
            if (colorRenderer == null) return;

            var propertyBlock = new MaterialPropertyBlock();
            colorRenderer.GetPropertyBlock(propertyBlock);
            var color = ChannelVisual.ToUnityColor(useRequiredColorForVisual ? requiredColor : visualColor);
            propertyBlock.SetColor("_BaseColor", color);
            propertyBlock.SetColor("_Color", color);
            propertyBlock.SetColor("_FillColor", color);
            colorRenderer.SetPropertyBlock(propertyBlock);
        }

        public void ResetFlow() => CurrentColor = ColorType.None; // разрушенный блок сбросом не восстанавливается

        public ColorType ReceiveFlow(ColorType incoming)
        {
            CurrentColor = incoming;
            if (!IsDestroyed && incoming.Matches(requiredColor))
            {
                IsDestroyed = true;
                blockedChannel?.ClearFillBarrier();

                // Keep the logic node alive so it can pass flow to its optional
                // output, but remove only the visible obstacle.
                if (blockVisual != null)
                    blockVisual.SetActive(false);

                OnDestroyedByFlow?.Invoke();
            }
            return CurrentColor;
        }

        public IEnumerable<IFlowNode> GetActiveOutputs()
        {
            // A wrong colour remains a hard stop. Once melted, this can open a
            // continuation placed after the block; leave Output empty for a
            // block that finishes a route.
            if (!IsDestroyed)
                yield break;

            if (_runtimeOutput != null)
            {
                yield return _runtimeOutput;
                yield break;
            }

            if (output != null && output.Node != null)
                yield return output.Node;
        }
    }
}
