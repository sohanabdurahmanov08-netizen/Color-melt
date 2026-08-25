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
        private List<IFlowNode> _runtimePositions;

        public int CurrentPositionIndex => _currentPositionIndex;
        public bool CanCycle => (_runtimePositions != null ? _runtimePositions.Count : positions?.Count ?? 0) > 1;
        public ColorType CurrentColor { get; private set; } = ColorType.None;

        /// <summary>Подпишитесь, чтобы проиграть анимацию поворота рычага.</summary>
        public System.Action<int> OnPositionChanged;

        private void Awake() => _currentPositionIndex = defaultPositionIndex;

        /// <summary>
        /// Assigns the selectable destinations for a generated triangular lever.
        /// The first destination is always the route's own channel.
        /// </summary>
        public void ConfigureRuntimePositions(List<IFlowNode> destinations, int defaultIndex = 0)
        {
            _runtimePositions = destinations;
            _currentPositionIndex = destinations == null || destinations.Count == 0
                ? 0
                : Mathf.Clamp(defaultIndex, 0, destinations.Count - 1);
            OnPositionChanged?.Invoke(_currentPositionIndex);
        }

        /// <summary>Переключить рычаг на следующую позицию — это действие игрока, тратит ход.</summary>
        public void CyclePosition()
        {
            var count = _runtimePositions != null ? _runtimePositions.Count : positions?.Count ?? 0;
            if (count == 0) return;
            _currentPositionIndex = (_currentPositionIndex + 1) % count;
            OnPositionChanged?.Invoke(_currentPositionIndex);
        }

        public void SetPosition(int index)
        {
            var count = _runtimePositions != null ? _runtimePositions.Count : positions?.Count ?? 0;
            _currentPositionIndex = count == 0 ? 0 : Mathf.Clamp(index, 0, count - 1);
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
            if (_runtimePositions != null)
            {
                if (_runtimePositions.Count > 0 && _runtimePositions[_currentPositionIndex] != null)
                    yield return _runtimePositions[_currentPositionIndex];
                yield break;
            }

            if (positions == null || positions.Count == 0) yield break;
            var active = positions[_currentPositionIndex];
            if (active != null && active.Node != null)
                yield return active.Node;
        }
    }
}
