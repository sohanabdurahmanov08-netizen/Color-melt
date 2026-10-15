using System;
using UnityEngine;

namespace ColorMelt.Gameplay
{
    /// <summary>
    /// Paint pouring off the end of a channel once nothing blocks it any
    /// more. The path is ballistic: the paint leaves the outlet along the
    /// channel and falls under gravity. A camera-facing ribbon mesh follows
    /// it and narrows as the paint speeds up (the same amount of paint per
    /// second through a faster stream makes it thinner); the ColorMelt/
    /// PaintFall shader adds the wobble and the break-up into drops.
    ///
    /// Arriving paint grows a rounded front from the outlet; when the paint
    /// stops, the stream's top end leaves the outlet and falls after it.
    /// The win sequence lets it fall much further (MaxTime) and can clip it
    /// at a screen line, where the UI jar takes the paint over.
    /// </summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class OutletFall : MonoBehaviour
    {
        private const int Segments = 40;

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int SeedId = Shader.PropertyToID("_Seed");
        private static readonly int BreakupId = Shader.PropertyToID("_Breakup");

        [Tooltip("Stream width at the outlet, in board units.")]
        [SerializeField, Min(0f)] private float width = 0.95f;
        [Tooltip("Thinnest the stream gets as it speeds up, as a share of its outlet width.")]
        [SerializeField, Range(0.1f, 1f)] private float minWidth = 0.35f;
        [Tooltip("Speed the paint leaves the outlet with, in board units per second.")]
        [SerializeField, Min(0.1f)] private float launchSpeed = 3f;
        [SerializeField, Min(0f)] private float gravity = 30f;
        [Tooltip("Seconds of fall shown while playing; the win sequence raises it.")]
        [SerializeField, Min(0.05f)] private float maxTime = 0.6f;
        [Tooltip("Length of the rounded front and tail, in seconds of fall.")]
        [SerializeField, Min(0.005f)] private float capTime = 0.05f;

        private readonly Vector3[] _points = new Vector3[Segments];
        private readonly float[] _times = new float[Segments];
        private readonly Vector3[] _vertices = new Vector3[Segments * 2];
        private readonly Vector2[] _uvs = new Vector2[Segments * 2];
        private Mesh _mesh;
        private MeshRenderer _renderer;
        private MaterialPropertyBlock _block;
        private Camera _camera;
        private Vector3 _start;
        private Vector3 _direction;
        private float _unit = 1f;
        private float _head;   // seconds of fall below the outlet
        private float _tail;   // seconds drained from the top
        private bool _flowing;
        private bool _clipped;
        private float _breakup = -1f;

        /// <summary>How long a drop is shown falling. Raised by the win sequence.</summary>
        public float MaxTime
        {
            get => maxTime;
            set => maxTime = Mathf.Max(0.05f, value);
        }

        /// <summary>Optional cut-off: points for which it returns true are not drawn.</summary>
        public Func<Vector3, bool> Clip { get; set; }

        public bool IsVisible => _renderer != null && _renderer.enabled;

        /// <summary>Lowest point drawn this frame (where a clip cut the stream).</summary>
        public Vector3 EndPoint { get; private set; }

        public Color Color { get; private set; }

        /// <summary>Multiplies the stream width, matching channels widened by LevelBuilder.</summary>
        public float WidthScale { get; set; } = 1f;

        /// <summary>World width of the stream at EndPoint.</summary>
        public float EndWidth { get; private set; }

        /// <summary>Thinnest the stream gets, as a share of its outlet width (the win pour keeps it fuller).</summary>
        public float MinWidth
        {
            get => minWidth;
            set => minWidth = Mathf.Clamp(value, 0.1f, 1f);
        }

        /// <summary>Overrides how much the stream breaks into drops (0 = a steady pour). Negative = material default.</summary>
        public float Breakup
        {
            get => _breakup;
            set
            {
                _breakup = value;
                ApplyBlock();
            }
        }

        private void Awake()
        {
            _mesh = new Mesh { name = "Outlet fall" };
            _mesh.MarkDynamic();
            var triangles = new int[(Segments - 1) * 6];
            for (var i = 0; i < Segments - 1; i++)
            {
                var v = i * 2;
                var t = i * 6;
                triangles[t] = v;
                triangles[t + 1] = v + 2;
                triangles[t + 2] = v + 1;
                triangles[t + 3] = v + 1;
                triangles[t + 4] = v + 2;
                triangles[t + 5] = v + 3;
            }
            _mesh.vertices = _vertices;
            _mesh.uv = _uvs;
            _mesh.triangles = triangles;
            GetComponent<MeshFilter>().sharedMesh = _mesh;

            _renderer = GetComponent<MeshRenderer>();
            _renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _renderer.receiveShadows = false;
            _renderer.enabled = false;
            _block = new MaterialPropertyBlock();
            _block.SetFloat(SeedId, UnityEngine.Random.value * 10f);
        }

        private void OnDestroy()
        {
            if (_mesh != null) Destroy(_mesh);
        }

        /// <param name="unit">World size of one board unit (the board is scaled).</param>
        public void Flow(Vector3 outlet, Vector3 direction, Color color, float unit)
        {
            if (!_flowing)
            {
                // Paint arriving again (or for the first time) leaves the outlet afresh.
                if (_tail >= _head) _head = 0f;
                _tail = 0f;
            }
            _flowing = true;
            _start = outlet;
            _direction = direction.normalized;
            _unit = unit;
            if (color != Color)
            {
                Color = color;
                ApplyBlock();
            }
        }

        public void Stop() => _flowing = false;

        private void ApplyBlock()
        {
            if (_renderer == null) return;
            _block.SetColor(BaseColorId, Color);
            if (_breakup >= 0f) _block.SetFloat(BreakupId, _breakup);
            _renderer.SetPropertyBlock(_block);
        }

        private void LateUpdate()
        {
            if (_renderer == null || (!_flowing && !_renderer.enabled && _head <= _tail)) return;

            // The stream shows fall times tail..head: the head runs down from
            // the outlet, and once the paint stops the tail follows it.
            var dt = Time.deltaTime;
            _head = Mathf.Min(_head + dt, maxTime);
            if (!_flowing) _tail = Mathf.Min(_tail + dt, _head);

            if (_head - _tail <= 0.001f)
            {
                _renderer.enabled = false;
                return;
            }

            var count = SamplePath();
            if (count < 2)
            {
                _renderer.enabled = false;
                return;
            }

            BuildMesh(count);
            _renderer.enabled = true;
        }

        /// <summary>Points along the fall from tail to head, cut where Clip says.</summary>
        private int SamplePath()
        {
            _clipped = false;
            var count = 0;
            for (var i = 0; i < Segments; i++)
            {
                var t = Mathf.Lerp(_tail, _head, i / (Segments - 1f));
                var point = PointAt(t);
                if (Clip != null && Clip(point))
                {
                    if (i == 0) return 0;
                    // Finish the stream exactly on the cut.
                    t = Refine(_times[count - 1], t);
                    point = PointAt(t);
                    _points[count] = point;
                    _times[count++] = t;
                    _clipped = true;
                    break;
                }
                _points[count] = point;
                _times[count++] = t;
            }
            EndPoint = _points[count - 1];
            return count;
        }

        private void BuildMesh(int count)
        {
            if (_camera == null) _camera = Camera.main;
            var toCamera = _camera != null ? _camera.transform.position : _start + Vector3.back * 100f;
            var toLocal = transform.worldToLocalMatrix;
            var halfWidth = width * WidthScale * _unit * 0.5f;
            var outletSpeed = launchSpeed;

            for (var i = 0; i < Segments; i++)
            {
                // Unused tail of the strip collapses onto the last point.
                var index = Mathf.Min(i, count - 1);
                var point = _points[index];
                var time = _times[index];

                var tangent = index < count - 1 ? _points[index + 1] - point : point - _points[index - 1];
                var side = Vector3.Cross(tangent, toCamera - point).normalized;

                // Continuity: a faster stream carries the same paint in a thinner thread.
                var speed = Velocity(time).magnitude;
                var thickness = Mathf.Max(minWidth, Mathf.Sqrt(outletSpeed / Mathf.Max(speed, outletSpeed)));
                // Rounded ends: the falling front, and the top once the paint stops.
                if (!_clipped || index < count - 1)
                    thickness *= Cap((_head - time) / capTime);
                if (_tail > 0f)
                    thickness *= Cap((time - _tail) / capTime);
                if (i >= count) thickness = 0f;

                var offset = side * (halfWidth * thickness);
                if (i == count - 1) EndWidth = halfWidth * thickness * 2f;
                _vertices[i * 2] = toLocal.MultiplyPoint3x4(point - offset);
                _vertices[i * 2 + 1] = toLocal.MultiplyPoint3x4(point + offset);
                _uvs[i * 2] = new Vector2(time, 0f);
                _uvs[i * 2 + 1] = new Vector2(time, 1f);
            }

            _mesh.vertices = _vertices;
            _mesh.uv = _uvs;
            _mesh.RecalculateBounds();
        }

        private static float Cap(float x) => x >= 1f ? 1f : Mathf.Sqrt(Mathf.Clamp01(x * (2f - x)));

        private Vector3 Velocity(float time) => _direction * launchSpeed + Vector3.down * (gravity * time);

        private Vector3 PointAt(float time) =>
            _start + _direction * (launchSpeed * _unit * time) + Vector3.down * (0.5f * gravity * _unit * time * time);

        private float Refine(float insideTime, float outsideTime)
        {
            for (var step = 0; step < 6; step++)
            {
                var mid = (insideTime + outsideTime) * 0.5f;
                if (Clip(PointAt(mid))) outsideTime = mid;
                else insideTime = mid;
            }
            return insideTime;
        }
    }
}
