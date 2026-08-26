using UnityEngine;

namespace ColorMelt.Core
{
    /// <summary>Click target and visual animator for a generated cube lever.</summary>
    [RequireComponent(typeof(Collider))]
    public class RouteSwitchLever : MonoBehaviour
    {
        [SerializeField, Min(1f)] private float turnSpeed = 540f;

        private static RouteSwitchLever _selectedLever;
        private LevelFlowManager _flowManager;
        private SwitchNode _switchNode;
        private Renderer _renderer;
        private MaterialPropertyBlock _visualProperties;
        private Color _normalColor = Color.white;
        private float[] _positionYaws;
        private Quaternion _targetRotation = Quaternion.identity;
        private int _lastPressFrame = -1;

        public void Configure(LevelFlowManager flowManager, SwitchNode switchNode, float[] positionYaws,
            Color normalColor)
        {
            if (_switchNode != null)
                _switchNode.OnPositionChanged -= HandlePositionChanged;

            _flowManager = flowManager;
            _switchNode = switchNode;
            _positionYaws = positionYaws;
            _normalColor = normalColor;
            _renderer = GetComponent<Renderer>();
            _visualProperties = new MaterialPropertyBlock();
            ApplySelectionVisual(_selectedLever == this);
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
            if (_selectedLever == this)
            {
                ApplySelectionVisual(false);
                _selectedLever = null;
            }
        }

        private void OnMouseDown() => HandleLeverPressed();

        private void Update()
        {
            transform.localRotation = Quaternion.RotateTowards(transform.localRotation, _targetRotation,
                turnSpeed * Time.deltaTime);
        }

        // This temporary desktop control deliberately uses IMGUI key events
        // instead of Input.GetKeyDown. It keeps working whether the project
        // uses the old Input Manager, the new Input System, or both.
        private void OnGUI()
        {
            if (_selectedLever != this) return;

            var inputEvent = Event.current;
            if (inputEvent.type != EventType.KeyDown) return;

            if (inputEvent.keyCode == KeyCode.LeftArrow || inputEvent.character == '<')
            {
                SelectDirection(-1);
                ClearSelection();
                inputEvent.Use();
            }
            else if (inputEvent.keyCode == KeyCode.RightArrow || inputEvent.character == '>')
            {
                SelectDirection(1);
                ClearSelection();
                inputEvent.Use();
            }
            else if (inputEvent.keyCode == KeyCode.UpArrow)
            {
                _flowManager?.OnPlayerSelectedSwitchPosition(_switchNode, 0);
                ClearSelection();
                inputEvent.Use();
            }
        }

        private void HandleLeverPressed()
        {
            if (_lastPressFrame == Time.frameCount) return;
            _lastPressFrame = Time.frameCount;

            if (_switchNode == null || _positionYaws == null || !_switchNode.CanCycle) return;

            // An edge hill has exactly one alternative. It remains a direct,
            // one-tap control: each press toggles between its own route and
            // the only neighbouring route.
            if (!CanChooseBothDirections())
            {
                _flowManager?.OnPlayerToggledSwitch(_switchNode);
                return;
            }

            // A middle hill waits for an explicit keyboard direction. This
            // maps neatly to future mobile UI buttons without changing logic.
            if (_selectedLever != null && _selectedLever != this)
                _selectedLever.ApplySelectionVisual(false);

            _selectedLever = this;
            ApplySelectionVisual(true);
        }

        private void ClearSelection()
        {
            if (_selectedLever != this) return;
            ApplySelectionVisual(false);
            _selectedLever = null;
        }

        private void ApplySelectionVisual(bool selected)
        {
            if (_renderer == null) return;

            _renderer.GetPropertyBlock(_visualProperties);
            var displayColor = selected ? Color.Lerp(_normalColor, Color.white, 0.45f) : _normalColor;
            _visualProperties.SetColor("_BaseColor", displayColor);
            _visualProperties.SetColor("_Color", displayColor);
            // URP/Lit uses this property when emission is enabled; shaders
            // that do not support it simply ignore it, retaining the brighter
            // base colour above.
            _visualProperties.SetColor("_EmissionColor", selected ? _normalColor * 1.2f : Color.black);
            _renderer.SetPropertyBlock(_visualProperties);
        }

        private bool CanChooseBothDirections()
        {
            return FindPositionForDirection(-1) > 0 && FindPositionForDirection(1) > 0;
        }

        private void SelectDirection(int direction)
        {
            var positionIndex = FindPositionForDirection(direction);
            if (positionIndex > 0)
                _flowManager?.OnPlayerSelectedSwitchPosition(_switchNode, positionIndex);
        }

        private int FindPositionForDirection(int direction)
        {
            for (var positionIndex = 1; positionIndex < _positionYaws.Length; positionIndex++)
            {
                var yaw = _positionYaws[positionIndex];
                if ((direction < 0 && yaw < 0f) || (direction > 0 && yaw > 0f))
                    return positionIndex;
            }

            return 0;
        }

        private void HandlePositionChanged(int positionIndex)
        {
            var yaw = _positionYaws != null && positionIndex >= 0 && positionIndex < _positionYaws.Length
                ? _positionYaws[positionIndex]
                : 0f;
            _targetRotation = Quaternion.Euler(0f, yaw, 0f);
        }
    }
}
