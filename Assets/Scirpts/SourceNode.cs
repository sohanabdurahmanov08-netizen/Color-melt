using System.Collections.Generic;
using UnityEngine;

namespace ColorMelt.Core
{
    /// <summary>Источник — всегда выдаёт один и тот же цвет, вход игнорирует.</summary>
    public class SourceNode : MonoBehaviour, IFlowNode
    {
        [SerializeField] private ColorType sourceColor;
        [SerializeField] private FlowNodeRef output;

        public ColorType CurrentColor => sourceColor;

        public void ResetFlow() { /* у источника нет состояния для сброса */ }

        public ColorType ReceiveFlow(ColorType incoming) => sourceColor;

        public IEnumerable<IFlowNode> GetActiveOutputs()
        {
            if (output != null && output.Node != null)
                yield return output.Node;
        }
    }
}
