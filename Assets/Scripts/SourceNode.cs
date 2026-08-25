using System.Collections.Generic;
using UnityEngine;

namespace ColorMelt.Core
{
    /// <summary>Источник — всегда выдаёт один и тот же цвет, вход игнорирует.</summary>
    public class SourceNode : MonoBehaviour, IFlowNode
    {
        [SerializeField] private ColorType sourceColor;
        [SerializeField] private FlowNodeRef output;

        private IFlowNode _runtimeOutput;

        public ColorType CurrentColor => sourceColor;

        /// <summary>Sets up a source created by Level1RouteGenerator at runtime.</summary>
        public void Configure(ColorType color, IFlowNode nextNode)
        {
            sourceColor = color;
            _runtimeOutput = nextNode;
        }

        public void ResetFlow() { /* у источника нет состояния для сброса */ }

        public ColorType ReceiveFlow(ColorType incoming) => sourceColor;

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
