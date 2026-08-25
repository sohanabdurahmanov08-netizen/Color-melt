using System.Collections.Generic;
using UnityEngine;

namespace ColorMelt.Core
{
    /// <summary>
    /// Обычный канал (труба). Хранит текущий цвет для визуализации заполнения
    /// (шейдер fill-amount, партиклы и т.д.) и передаёт поток на фиксированный выход.
    /// </summary>
    public class ChannelNode : MonoBehaviour, IFlowNode
    {
        [SerializeField] private FlowNodeRef output;

        public ColorType CurrentColor { get; private set; } = ColorType.None;

        /// <summary>Подпишитесь на это событие в скрипте визуала канала.</summary>
        public System.Action<ColorType> OnFlowChanged;

        private IFlowNode _runtimeOutput;

        /// <summary>Connects generated level pieces without changing the prefab asset.</summary>
        public void SetRuntimeOutput(IFlowNode nextNode) => _runtimeOutput = nextNode;

        public void ResetFlow()
        {
            CurrentColor = ColorType.None;
            OnFlowChanged?.Invoke(CurrentColor);
        }

        public ColorType ReceiveFlow(ColorType incoming)
        {
            CurrentColor = CurrentColor.Mix(incoming);
            OnFlowChanged?.Invoke(CurrentColor);
            return CurrentColor;
        }

        public IEnumerable<IFlowNode> GetActiveOutputs()
        {
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
