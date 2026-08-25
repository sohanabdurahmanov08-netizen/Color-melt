using System.Collections.Generic;
using UnityEngine;

namespace ColorMelt.Core
{
    /// <summary>
    /// Базовый интерфейс любого узла системы потоков: источник, канал, рычаг, блок.
    /// LevelFlowManager обходит весь граф уровня именно через этот интерфейс,
    /// поэтому новые типы узлов добавляются без изменения логики симуляции.
    /// </summary>
    public interface IFlowNode
    {
        ColorType CurrentColor { get; }

        /// <summary>Сбросить состояние узла (перед пересчётом всей системы).</summary>
        void ResetFlow();

        /// <summary>Принять входящий поток и вернуть итоговый цвет узла после смешивания.</summary>
        ColorType ReceiveFlow(ColorType incoming);

        /// <summary>Активные выходы, через которые поток идёт дальше (пусто для тупика/блока).</summary>
        IEnumerable<IFlowNode> GetActiveOutputs();
    }

    /// <summary>
    /// Обёртка-ссылка на узел для инспектора Unity.
    /// Интерфейсы нельзя сериализовать напрямую, поэтому перетаскиваем сюда
    /// любой GameObject с компонентом-узлом (Source/Channel/Switch/Block).
    /// Это не MonoBehaviour, поэтому предупреждение "no MonoBehaviour scripts"
    /// для этого файла не появится.
    /// </summary>
    [System.Serializable]
    public class FlowNodeRef
    {
        [SerializeField] private MonoBehaviour nodeComponent;
        public IFlowNode Node => nodeComponent as IFlowNode;
    }
}
