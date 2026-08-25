using System.Collections.Generic;
using UnityEngine;

namespace ColorMelt.Core
{
    /// <summary>
    /// Рычаг. Имеет несколько позиций, каждая ведёт к своему каналу.
    /// Активна только одна позиция — именно она определяет маршрут потока.
    /// </summary>
    public class SwitchNode : MonoBehaviour, IFlowNode
    {
        [SerializeField] private List<FlowNodeRef> positions;
        [SerializeField] private int defaultPositionIndex = 0;

        private int _currentPositionIndex;

        public int CurrentPositionIndex => _currentPositionIndex;
        public ColorType CurrentColor { get; private set; } = ColorType.None;

        /// <summary>Подпишитесь, чтобы проиграть анимацию поворота рычага.</summary>
        public System.Action<int> OnPositionChanged;

        private void Awake() => _currentPositionIndex = defaultPositionIndex;

        /// <summary>Переключить рычаг на следующую позицию — это действие игрока, тратит ход.</summary>
        public void CyclePosition()
        {
            if (positions == null || positions.Count == 0) return;
            _currentPositionIndex = (_currentPositionIndex + 1) % positions.Count;
            OnPositionChanged?.Invoke(_currentPositionIndex);
        }

        public void SetPosition(int index)
        {
            _currentPositionIndex = index;
            OnPositionChanged?.Invoke(_currentPositionIndex);
        }

        /// <summary>Возврат в исходное положение — автосброс после разрушения блока, ход не тратится.</summary>
        public void ResetPosition()
        {
            _currentPositionIndex = defaultPositionIndex;
            OnPositionChanged?.Invoke(_currentPositionIndex);
        }

        public void ResetFlow() => CurrentColor = ColorType.None;

        public ColorType ReceiveFlow(ColorType incoming)
        {
            CurrentColor = incoming;
            return CurrentColor;
        }

        public IEnumerable<IFlowNode> GetActiveOutputs()
        {
            if (positions == null || positions.Count == 0) yield break;
            var active = positions[_currentPositionIndex];
            if (active != null && active.Node != null)
                yield return active.Node;
        }
    }
}
