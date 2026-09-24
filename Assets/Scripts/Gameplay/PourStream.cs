using UnityEngine;

namespace ColorMelt.Gameplay
{
    /// <summary>
    /// A wobbling arc of paint from a route's source into a neighbouring
    /// channel. It makes a redirected flow readable at a glance.
    /// </summary>
    [RequireComponent(typeof(LineRenderer))]
    public class PourStream : MonoBehaviour
    {
        private const int Segments = 24;

        [SerializeField, Min(0f)] private float arcHeight = 1.2f;
        [SerializeField, Min(0f)] private float width = 0.45f;
        [SerializeField, Min(0.05f)] private float growTime = 0.25f;

        private LineRenderer _line;
        private Vector3 _from;
        private Vector3 _to;
        private float _progress;
        private bool _visible;
        private float _unit = 1f;

        private void Awake()
        {
            _line = GetComponent<LineRenderer>();
            _line.positionCount = Segments;
            _line.useWorldSpace = true;
            _line.numCapVertices = 4;
            _line.enabled = false;
        }

        /// <param name="unit">World size of one board unit (the board is scaled).</param>
        public void Show(Vector3 from, Vector3 to, Color color, float unit)
        {
            if (!_visible || (_to - to).sqrMagnitude > 0.01f)
                _progress = 0f;

            _from = from;
            _to = to;
            _unit = unit;
            _visible = true;
            _line.enabled = true;
            _line.startColor = color;
            _line.endColor = Color.Lerp(color, Color.white, 0.15f);
        }

        public void Hide()
        {
            _visible = false;
        }

        private void Update()
        {
            if (_line == null) return;

            _progress = Mathf.MoveTowards(_progress, _visible ? 1f : 0f, Time.deltaTime / growTime);
            if (_progress <= 0f)
            {
                _line.enabled = false;
                return;
            }

            var wobble = 1f + 0.12f * Mathf.Sin(Time.time * 14f);
            _line.widthMultiplier = width * _unit * wobble;

            for (var i = 0; i < Segments; i++)
            {
                var t = i / (Segments - 1f) * _progress;
                var point = Vector3.Lerp(_from, _to, t);
                point += Vector3.up * (Mathf.Sin(t * Mathf.PI) * arcHeight * _unit);
                _line.SetPosition(i, point);
            }
        }
    }
}
