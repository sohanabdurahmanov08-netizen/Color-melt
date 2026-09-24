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
            if (_source != null)
                _source.localRotation = Quaternion.Slerp(_source.localRotation, _sourceTarget,
                    1f - Mathf.Exp(-10f * Time.deltaTime));
        }

        /// <summary>Screen-space bounds of every visible part of the route.</summary>
        public bool TryGetScreenRect(Camera cam, out Rect rect)
        {
            var hasPoint = false;
            var min = Vector2.zero;
            var max = Vector2.zero;
            var corners = new Vector3[8];

            foreach (var renderer in _renderers)
            {
                if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy ||
                    renderer is LineRenderer || renderer is ParticleSystemRenderer)
                    continue;

                var b = renderer.bounds;
                for (var i = 0; i < 8; i++)
                    corners[i] = new Vector3((i & 1) == 0 ? b.min.x : b.max.x, (i & 2) == 0 ? b.min.y : b.max.y,
                        (i & 4) == 0 ? b.min.z : b.max.z);

                foreach (var corner in corners)
                {
                    var screen = cam.WorldToScreenPoint(corner);
                    if (screen.z <= 0f) continue;

                    if (!hasPoint)
                    {
                        min = max = screen;
                        hasPoint = true;
                    }
                    else
                    {
                        min = Vector2.Min(min, screen);
                        max = Vector2.Max(max, screen);
                    }
                }
            }

            rect = Rect.MinMaxRect(min.x, min.y, max.x, max.y);
            return hasPoint;
        }
    }
}
