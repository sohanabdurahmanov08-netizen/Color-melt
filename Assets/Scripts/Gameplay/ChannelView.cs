using ColorMelt.Core;
using UnityEngine;

namespace ColorMelt.Gameplay
{
    public enum ChannelHighlight
    {
        None,
        Selected,
        Target,
        Hint
    }

    /// <summary>
    /// Visual of one channel model (Color_route prefab). Animates the liquid
    /// shader (_FillAmount / _FillColor), pulses the channel body while it is
    /// selected or offered as a pour target, and maps a 0..1 position along
    /// the channel to a point on the liquid surface for placing blocks.
    ///
    /// Liquid shader requirement: UV.x runs along the channel, 0 at the inlet.
    /// </summary>
    public class ChannelView : MonoBehaviour
    {
        private const int PathSamples = 21;

        [SerializeField] private Renderer liquidRenderer;
        [SerializeField] private Renderer bodyRenderer;

        [Header("Liquid")]
        [SerializeField, Min(0.1f)] private float fillSpeed = 1.6f;
        [SerializeField, Min(0.1f)] private float colorBlendSpeed = 4f;

        [Header("Highlight")]
        [SerializeField, Min(0f)] private float pulseSpeed = 7f;
        [SerializeField, Min(0f)] private float selectedLift = 0.35f;

        [SerializeField, HideInInspector] private Vector3[] path;

        private static readonly int FillAmountId = Shader.PropertyToID("_FillAmount");
        private static readonly int FillColorId = Shader.PropertyToID("_FillColor");
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        private MaterialPropertyBlock _liquidBlock;
        private MaterialPropertyBlock _bodyBlock;
        private Color _bodyColor = Color.gray;
        private Color _accentColor = Color.white;
        private Color _currentColor = Color.white;
        private Color _targetColor = Color.white;
        private float _currentFill;
        private float _targetFill;
        private float _fillLimit = 1f;
        private bool _hasPaint;
        private ChannelHighlight _highlight;
        private float _highlightWeight;
        private Vector3 _restLocalPosition;
        private bool _initialized;

        public ChannelHighlight Highlight => _highlight;
        public Renderer BodyRenderer => bodyRenderer;

        private void Awake() => Initialize();

        private void Initialize()
        {
            if (_initialized) return;
            _initialized = true;

            _liquidBlock = new MaterialPropertyBlock();
            _bodyBlock = new MaterialPropertyBlock();
            _restLocalPosition = transform.localPosition;

            if (bodyRenderer != null && bodyRenderer.sharedMaterial != null &&
                bodyRenderer.sharedMaterial.HasProperty(BaseColorId))
                _bodyColor = bodyRenderer.sharedMaterial.GetColor(BaseColorId);

            ApplyLiquid();
            ApplyBody(0f);
        }

        /// <summary>Colour used for the highlight pulse (the route's own paint).</summary>
        public void SetAccent(ColorType color) => _accentColor = color.ToUnityColor();

        public void SetPaint(ColorType color)
        {
            Initialize();

            if (color.IsEmpty())
            {
                _hasPaint = false;
                _targetFill = 0f;
                return;
            }

            _targetColor = color.ToUnityColor();
            if (!_hasPaint)
            {
                // Fresh paint flows in from the inlet in its own colour.
                _currentColor = _targetColor;
                if (_currentFill <= 0.01f)
                    _currentFill = 0f;
            }

            _hasPaint = true;
            _targetFill = _fillLimit;
        }

        /// <summary>Caps the liquid at a block (0..1 along the channel).</summary>
        public void SetFillLimit(float limit)
        {
            _fillLimit = Mathf.Clamp01(limit);
            if (_hasPaint)
                _targetFill = _fillLimit;
        }

        /// <summary>Shows paint instantly (used when a level is first built).</summary>
        public void SnapToTarget()
        {
            _currentFill = _targetFill;
            _currentColor = _targetColor;
            ApplyLiquid();
        }

        public void SetHighlight(ChannelHighlight highlight) => _highlight = highlight;

        /// <summary>World point on the liquid surface, t = 0 inlet .. 1 outlet.</summary>
        public Vector3 GetPoint(float t)
        {
            if (path == null || path.Length < 2)
                return transform.position;

            var scaled = Mathf.Clamp01(t) * (path.Length - 1);
            var index = Mathf.Min(Mathf.FloorToInt(scaled), path.Length - 2);
            var local = Vector3.Lerp(path[index], path[index + 1], scaled - index);
            return transform.TransformPoint(local);
        }

        private void Update()
        {
            _currentFill = Mathf.MoveTowards(_currentFill, _targetFill, fillSpeed * Time.deltaTime);
            _currentColor = Color.Lerp(_currentColor, _targetColor, 1f - Mathf.Exp(-colorBlendSpeed * Time.deltaTime));
            ApplyLiquid();

            var targetWeight = _highlight == ChannelHighlight.None ? 0f : 1f;
            _highlightWeight = Mathf.MoveTowards(_highlightWeight, targetWeight, Time.deltaTime * 6f);

            var pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * pulseSpeed);
            float strength;
            switch (_highlight)
            {
                case ChannelHighlight.Selected: strength = 0.75f; break;
                case ChannelHighlight.Target: strength = 0.25f + 0.4f * pulse; break;
                case ChannelHighlight.Hint: strength = 0.2f + 0.8f * pulse; break;
                default: strength = 0.6f; break;
            }
            ApplyBody(strength * _highlightWeight);

            var lift = _highlight == ChannelHighlight.Selected ? selectedLift : 0f;
            var targetPosition = _restLocalPosition + Vector3.up * lift;
            transform.localPosition = Vector3.Lerp(transform.localPosition, targetPosition,
                1f - Mathf.Exp(-12f * Time.deltaTime));
        }

        private void ApplyLiquid()
        {
            if (liquidRenderer == null) return;

            // Property blocks are not serialized, so recreate them after a
            // script reload in play mode.
            _liquidBlock ??= new MaterialPropertyBlock();
            liquidRenderer.GetPropertyBlock(_liquidBlock);
            _liquidBlock.SetFloat(FillAmountId, _currentFill);
            _liquidBlock.SetColor(FillColorId, _currentColor);
            liquidRenderer.SetPropertyBlock(_liquidBlock);
        }

        private void ApplyBody(float amount)
        {
            if (bodyRenderer == null) return;

            _bodyBlock ??= new MaterialPropertyBlock();
            var glow = Color.Lerp(_accentColor, Color.white, 0.35f);
            var color = Color.Lerp(_bodyColor, glow, amount);
            bodyRenderer.GetPropertyBlock(_bodyBlock);
            _bodyBlock.SetColor(BaseColorId, color);
            _bodyBlock.SetColor(ColorId, color);
            bodyRenderer.SetPropertyBlock(_bodyBlock);
        }

        /// <summary>
        /// Samples the liquid mesh: averages vertex positions per UV.x bin so
        /// blocks can be placed at any position along the curved channel.
        /// Runs in the editor; the result is serialized with the prefab.
        /// </summary>
        [ContextMenu("Bake Path From Liquid Mesh")]
        public void BakePath()
        {
            var filter = liquidRenderer != null ? liquidRenderer.GetComponent<MeshFilter>() : null;
            if (filter == null || filter.sharedMesh == null)
            {
                Debug.LogError("ChannelView: assign a liquid renderer with a mesh to bake the path.", this);
                return;
            }

            var mesh = filter.sharedMesh;
            var vertices = mesh.vertices;
            var uvs = mesh.uv;
            var sums = new Vector3[PathSamples];
            var counts = new int[PathSamples];
            var toRoot = transform.worldToLocalMatrix * filter.transform.localToWorldMatrix;

            for (var i = 0; i < vertices.Length && i < uvs.Length; i++)
            {
                var bin = Mathf.Clamp(Mathf.RoundToInt(uvs[i].x * (PathSamples - 1)), 0, PathSamples - 1);
                sums[bin] += toRoot.MultiplyPoint3x4(vertices[i]);
                counts[bin]++;
            }

            path = new Vector3[PathSamples];
            for (var i = 0; i < PathSamples; i++)
            {
                if (counts[i] > 0)
                {
                    path[i] = sums[i] / counts[i];
                    continue;
                }

                // Fill gaps from the nearest sampled neighbours.
                int before = i - 1, after = i + 1;
                while (before >= 0 && counts[before] == 0) before--;
                while (after < PathSamples && counts[after] == 0) after++;
                if (before >= 0 && after < PathSamples)
                    path[i] = Vector3.Lerp(sums[before] / counts[before], sums[after] / counts[after],
                        (float)(i - before) / (after - before));
                else if (before >= 0)
                    path[i] = sums[before] / counts[before];
                else if (after < PathSamples)
                    path[i] = sums[after] / counts[after];
            }
        }

        public void AssignRenderers(Renderer liquid, Renderer body)
        {
            liquidRenderer = liquid;
            bodyRenderer = body;
        }

        private void OnDrawGizmosSelected()
        {
            if (path == null) return;
            Gizmos.color = Color.cyan;
            for (var i = 0; i < path.Length - 1; i++)
                Gizmos.DrawLine(transform.TransformPoint(path[i]), transform.TransformPoint(path[i + 1]));
        }
    }
}
