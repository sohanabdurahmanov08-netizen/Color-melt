using System.Collections.Generic;
using ColorMelt.Core;
using UnityEngine;

namespace ColorMelt.Gameplay
{
    /// <summary>
    /// One built route: its channel, the paint source above the inlet, the
    /// blocks along the channel and the stream shown when it pours sideways.
    /// Created by LevelBuilder.
    /// </summary>
    public class RouteView : MonoBehaviour
    {
        private const float TapPoint = 0.35f;
        private const float BlockHalfLength = 0.03f;

        private readonly List<BlockView> _blocks = new List<BlockView>();
        private readonly List<float> _blockPositions = new List<float>();
        private Renderer[] _renderers;
        private Transform _source;
        private Quaternion _sourceRest;
        private Quaternion _sourceTarget;
        private float _unit;

        public int Index { get; private set; }
        public ColorType SourceColor { get; private set; }
        public ChannelView Channel { get; private set; }
        public PourStream Stream { get; private set; }
        public OutletFall Fall { get; private set; }
        public IReadOnlyList<BlockView> Blocks => _blocks;

        public void Initialize(int index, ColorType sourceColor, ChannelView channel, Transform source,
            PourStream stream, float unit)
        {
            Index = index;
            SourceColor = sourceColor;
            Channel = channel;
            Stream = stream;
            _source = source;
            _sourceRest = source != null ? source.localRotation : Quaternion.identity;
            _sourceTarget = _sourceRest;
            _unit = unit;
            channel.SetAccent(sourceColor);
        }

        public void SetFall(OutletFall fall) => Fall = fall;

        /// <summary>End of the channel, where free paint pours off.</summary>
        public Vector3 OutletPoint => Channel.GetPoint(1f);

        public Vector3 OutletDirection => (Channel.GetPoint(1f) - Channel.GetPoint(0.95f)).normalized;

        public void AddBlock(BlockView block, float position)
        {
            _blocks.Add(block);
            _blockPositions.Add(position);
        }

        public float BlockPosition(int index) => _blockPositions[index];

        /// <summary>Where the liquid stops in front of a block (its upper face).</summary>
        public float FillLimit(int index) => Mathf.Max(0f, _blockPositions[index] - BlockHalfLength);

        /// <summary>Called once building is finished so hit-testing sees every part.</summary>
        public void CacheRenderers() => _renderers = GetComponentsInChildren<Renderer>(true);

        /// <summary>World point the tutorial pointer and hint aim at.</summary>
        public Vector3 TapWorldPoint => Channel.GetPoint(TapPoint);

        public Vector3 InletPoint => Channel.GetPoint(0.02f);

        public Vector3 SourcePoint => _source != null ? _source.position : InletPoint;

        public void ShowPour(RouteView target)
        {
            if (target == null || target == this)
            {
                Stream.Hide();
                _sourceTarget = _sourceRest;
                return;
            }

            Stream.Show(SourcePoint, target.InletPoint, SourceColor.ToUnityColor(), _unit);
            // Tip the source towards the channel it pours into.
            // Rotating about local X tips the top towards +Z, where higher
            // route indices sit.
            var side = target.Index > Index ? 1f : -1f;
            _sourceTarget = _sourceRest * Quaternion.Euler(side * 30f, 0f, 0f);
        }

        private void Update()
        {
            if (_source != null && _source.localRotation != _sourceTarget)
                _source.localRotation = Quaternion.Slerp(_source.localRotation, _sourceTarget,
                    1f - Mathf.Exp(-10f * Time.deltaTime));

            // Paint that runs all the way down pours off the end.
            if (Fall == null) return;
            if (Channel.ReachesOutlet)
                Fall.Flow(OutletPoint, OutletDirection, Channel.PaintColor, _unit);
            else
                Fall.Stop();
        }

        private const int CentreLineSamples = 12;

        /// <summary>
        /// Screen distance from a point to the route's centre line, which runs
        /// from the paint source down the channel to its outlet. Tapping picks
        /// the nearest centre line, so every channel owns a wide strip of the
        /// screen however the perspective slants it.
        /// </summary>
        public float ScreenDistance(Camera cam, Vector2 point)
        {
            var best = float.MaxValue;
            var previous = (Vector2)cam.WorldToScreenPoint(SourcePoint);
            for (var i = 0; i <= CentreLineSamples; i++)
            {
                var next = (Vector2)cam.WorldToScreenPoint(Channel.GetPoint(i / (float)CentreLineSamples));
                var segment = next - previous;
                var along = segment.sqrMagnitude > 0f
                    ? Mathf.Clamp01(Vector2.Dot(point - previous, segment) / segment.sqrMagnitude)
                    : 0f;
                best = Mathf.Min(best, Vector2.Distance(point, previous + segment * along));
                previous = next;
            }
            return best;
        }

        /// <summary>World bounds of every visible part of the route.</summary>
        public bool TryGetWorldBounds(out Bounds bounds)
        {
            var hasBounds = false;
            bounds = default;

            foreach (var renderer in _renderers)
            {
                if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy ||
                    renderer is LineRenderer || renderer is ParticleSystemRenderer)
                    continue;

                if (!hasBounds)
                {
                    bounds = renderer.bounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(renderer.bounds);
                }
            }

            return hasBounds;
        }
    }
}
