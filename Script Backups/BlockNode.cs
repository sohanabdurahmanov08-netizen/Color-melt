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

        public bool IsDestroyed { get; private set; }
        public ColorType CurrentColor { get; private set; } = ColorType.None;

        /// <summary>Вызывается один раз, в момент разрушения — на него подписывается LevelFlowManager.</summary>
        public System.Action OnDestroyedByFlow;

        public void ResetFlow() => CurrentColor = ColorType.None; // разрушенный блок сбросом не восстанавливается

        public ColorType ReceiveFlow(ColorType incoming)
        {
            CurrentColor = incoming;
            if (!IsDestroyed && incoming.Matches(requiredColor))
            {
                IsDestroyed = true;
                OnDestroyedByFlow?.Invoke();
            }
            return CurrentColor;
        }

        public IEnumerable<IFlowNode> GetActiveOutputs()
        {
            yield break;
        }
    }
}
