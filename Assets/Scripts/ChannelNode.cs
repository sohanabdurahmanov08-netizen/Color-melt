using System.Collections.Generic;
using UnityEngine;

namespace ColorMelt.Core
{
    /// <summary>
    /// Канал, который собирает входящие цвета.
    ///
    /// Каждый новый поток смешивается с уже полученным цветом.
    /// В граф дальше передаётся именно итоговая смесь.
    /// </summary>
    public class ChannelNode : MonoBehaviour, IFlowNode
    {
        [SerializeField] private FlowNodeRef output;

        public ColorType CurrentColor { get; private set; } = ColorType.None;

        public System.Action<ColorType> OnFlowChanged;

        private IFlowNode _runtimeOutput;

        public void SetRuntimeOutput(IFlowNode nextNode)
        {
            _runtimeOutput = nextNode;
        }

        public void ResetFlow()
        {
            CurrentColor = ColorType.None;
            OnFlowChanged?.Invoke(CurrentColor);
        }

        public ColorType ReceiveFlow(ColorType incoming)
        {
            if (incoming.IsEmpty())
                return CurrentColor;

            var oldColor = CurrentColor;

            CurrentColor = CurrentColor.Mix(incoming);

            // Не вызываем визуальное обновление,
            // если цвет фактически не изменился.
            if (oldColor != CurrentColor)
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