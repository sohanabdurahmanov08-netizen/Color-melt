using UnityEngine;

namespace ColorMelt.Core
{
    /// <summary>Click target and visual animator for a generated triangular lever.</summary>
    [RequireComponent(typeof(Collider))]
    public class RouteSwitchLever : MonoBehaviour
    {
        private LevelFlowManager _flowManager;
        private SwitchNode _switchNode;

        public void Configure(LevelFlowManager flowManager, SwitchNode switchNode)
        {
            if (_switchNode != null)
                _switchNode.OnPositionChanged -= HandlePositionChanged;

            _flowManager = flowManager;
            _switchNode = switchNode;
            if (_switchNode != null)
            {
                _switchNode.OnPositionChanged += HandlePositionChanged;
                HandlePositionChanged(_switchNode.CurrentPositionIndex);
            }
        }

        private void OnDestroy()
        {
            if (_switchNode != null)
                _switchNode.OnPositionChanged -= HandlePositionChanged;
        }

        private void OnMouseDown() => _flowManager?.OnPlayerToggledSwitch(_switchNode);

        private void HandlePositionChanged(int positionIndex)
        {
            transform.localRotation = Quaternion.Euler(0f, positionIndex * 120f, 0f);
        }
    }
}
