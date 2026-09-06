using System.Collections.Generic;
using UnityEngine;

namespace ColorMelt.Core
{
    /// <summary>
    /// Рычаг с несколькими направлениями.
    /// Сам рычаг НЕ смешивает цвета.
    /// Он только передаёт дальше тот цвет, который получил.
    /// </summary>
    public class SwitchNode : MonoBehaviour, IFlowNode
    {
        [SerializeField] private List<FlowNodeRef> positions;
        [SerializeField] private int defaultPositionIndex = 0;

        private int _currentPositionIndex;
        private List<IFlowNode> _runtimePositions;

        public int CurrentPositionIndex => _currentPositionIndex;

        public bool CanCycle =>
            (_runtimePositions != null
                ? _runtimePositions.Count
                : positions?.Count ?? 0) > 1;

        public ColorType CurrentColor { get; private set; } = ColorType.None;

        public System.Action<int> OnPositionChanged;

        private void Awake()
        {
            _currentPositionIndex = defaultPositionIndex;
        }

        public void ConfigureRuntimePositions(
            List<IFlowNode> destinations,
            int defaultIndex = 0)
        {
            _runtimePositions = destinations;

            _currentPositionIndex =
                destinations == null || destinations.Count == 0
                    ? 0
                    : Mathf.Clamp(
                        defaultIndex,
                        0,
                        destinations.Count - 1
                    );

            OnPositionChanged?.Invoke(_currentPositionIndex);
        }

        public void CyclePosition()
        {
            var count =
                _runtimePositions != null
                    ? _runtimePositions.Count
                    : positions?.Count ?? 0;

            if (count == 0)
                return;

            _currentPositionIndex =
                (_currentPositionIndex + 1) % count;

            OnPositionChanged?.Invoke(_currentPositionIndex);
        }

        public void SetPosition(int index)
        {
            var count =
                _runtimePositions != null
                    ? _runtimePositions.Count
                    : positions?.Count ?? 0;

            if (count == 0)
            {
                _currentPositionIndex = 0;
            }
            else
            {
                _currentPositionIndex =
                    Mathf.Clamp(index, 0, count - 1);
            }

            OnPositionChanged?.Invoke(_currentPositionIndex);
        }

        public void ResetPosition()
        {
            _currentPositionIndex = defaultPositionIndex;
            OnPositionChanged?.Invoke(_currentPositionIndex);
        }

        public void ResetFlow()
        {
            CurrentColor = ColorType.None;
        }

        public ColorType ReceiveFlow(ColorType incoming)
        {
            // Switch не смешивает краску.
            // Он только пропускает получившийся цвет дальше.
            CurrentColor = incoming;
            return CurrentColor;
        }

        public IEnumerable<IFlowNode> GetActiveOutputs()
        {
            if (_runtimePositions != null)
            {
                if (_runtimePositions.Count > 0)
                {
                    var index = Mathf.Clamp(
                        _currentPositionIndex,
                        0,
                        _runtimePositions.Count - 1
                    );

                    var destination = _runtimePositions[index];

                    if (destination != null)
                        yield return destination;
                }

                yield break;
            }

            if (positions == null || positions.Count == 0)
                yield break;

            var activeIndex = Mathf.Clamp(
                _currentPositionIndex,
                0,
                positions.Count - 1
            );

            var active = positions[activeIndex];

            if (active != null && active.Node != null)
                yield return active.Node;
        }
    }
}