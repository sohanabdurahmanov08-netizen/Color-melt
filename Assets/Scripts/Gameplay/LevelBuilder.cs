using System.Collections.Generic;
using ColorMelt.Core;
using UnityEngine;

namespace ColorMelt.Gameplay
{
    /// <summary>
    /// Builds the board for a LevelData: one channel per route, laid out side
    /// by side and centred on Route Origin, a paint source above every inlet,
    /// blocks at their positions along the channel and a pour stream.
    ///
    /// All distances are in this object's local units, so the whole board can
    /// be moved or scaled by transforming this object.
    /// </summary>
    public class LevelBuilder : MonoBehaviour
    {
        private const string BoardName = "__Board";

        [Header("Prefabs")]
        [SerializeField] private ChannelView channelPrefab;
        [SerializeField] private BlockView blockPrefab;
        [Tooltip("Optional mesh for the paint source. A cylinder is used when empty.")]
        [SerializeField] private GameObject sourcePrefab;
        [SerializeField] private Material sourceMaterial;
        [SerializeField] private Material streamMaterial;
        [Tooltip("Paint pouring off free channel ends (ColorMelt/PaintFall).")]
        [SerializeField] private Material fallMaterial;

        [Header("Layout (local units)")]
        [Tooltip("Position of the middle route's channel.")]
        [SerializeField] private Vector3 routeOrigin = new Vector3(13.87f, 0f, -0.67f);
        [SerializeField, Min(0.5f)] private float routeSpacing = 4.4f;
        [Tooltip("Widens the channels (and their blocks, sources and falling paint) without making them longer, " +
                 "so they are easier to tap. Keep routeSpacing a good deal wider than 2 x this.")]
        [SerializeField, Range(1f, 2f)] private float channelWidth = 1.35f;
        [SerializeField] private float blockLift = 0.5f;
        [SerializeField] private float sourceHeight = 1.1f;
        [SerializeField] private Vector3 sourceScale = new Vector3(1.3f, 0.45f, 1.3f);

        /// <summary>World size of one local unit.</summary>
        public float Unit => transform.lossyScale.x;

        public List<RouteView> Build(LevelData level)
        {
            Clear();

            var board = new GameObject(BoardName).transform;
            board.SetParent(transform, false);
            if (!Application.isPlaying)
                board.gameObject.hideFlags = HideFlags.DontSave;

            var routes = new List<RouteView>();
            var count = level.routes.Count;
            for (var index = 0; index < count; index++)
            {
                var data = level.routes[index];
                var root = new GameObject($"Route {index + 1} ({data.color})").transform;
                root.SetParent(board, false);
                root.localPosition = routeOrigin + Vector3.forward * ((index - (count - 1) * 0.5f) * routeSpacing);

                var channel = Instantiate(channelPrefab, root);
                channel.name = "Channel";
                channel.transform.localPosition = Vector3.zero;
                // Routes sit side by side along Z, so Z is across the channel.
                channel.transform.localScale = Vector3.Scale(channel.transform.localScale, new Vector3(1f, 1f, channelWidth));

                var source = CreateSource(root, channel, data.color);
                var stream = CreateStream(root);
                var fall = CreateFall(root);

                var route = root.gameObject.AddComponent<RouteView>();
                route.Initialize(index, data.color, channel, source, stream, Unit);
                fall.WidthScale = channelWidth;
                route.SetFall(fall);

                foreach (var blockData in data.blocks)
                {
                    var block = Instantiate(blockPrefab, root);
                    block.name = $"Block ({blockData.color})";
                    block.transform.position = channel.GetPoint(blockData.position) + Vector3.up * (blockLift * channelWidth * Unit);
                    block.transform.localRotation = Quaternion.identity;
                    block.transform.localScale *= channelWidth;
                    block.Configure(blockData.color, blockData.inner);
                    route.AddBlock(block, blockData.position);
                }

                route.CacheRenderers();
                routes.Add(route);
            }

            return routes;
        }

        public void Clear()
        {
            var old = transform.Find(BoardName);
            while (old != null)
            {
                old.name = "__Destroyed";
                old.SetParent(null);
                if (Application.isPlaying)
                    Destroy(old.gameObject);
                else
                    DestroyImmediate(old.gameObject);
                old = transform.Find(BoardName);
            }
        }

        private Transform CreateSource(Transform root, ChannelView channel, ColorType color)
        {
            GameObject source;
            if (sourcePrefab != null)
            {
                source = Instantiate(sourcePrefab, root);
            }
            else
            {
                source = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                source.transform.SetParent(root, false);
                source.transform.localScale = Vector3.Scale(sourceScale, new Vector3(channelWidth, 1f, channelWidth));
                var generatedCollider = source.GetComponent<Collider>();
                if (Application.isPlaying) Destroy(generatedCollider);
                else DestroyImmediate(generatedCollider);
            }

            source.name = "Source";
            source.transform.position = channel.GetPoint(0f) + Vector3.up * (sourceHeight * Unit);

            var renderer = source.GetComponentInChildren<Renderer>();
            if (renderer != null)
            {
                if (sourceMaterial != null)
                    renderer.sharedMaterial = sourceMaterial;
                var block = new MaterialPropertyBlock();
                renderer.GetPropertyBlock(block);
                block.SetColor("_BaseColor", color.ToUnityColor());
                block.SetColor("_Color", color.ToUnityColor());
                renderer.SetPropertyBlock(block);
            }

            return source.transform;
        }

        private OutletFall CreateFall(Transform root)
        {
            var fallObject = new GameObject("Outlet fall", typeof(MeshFilter), typeof(MeshRenderer));
            fallObject.transform.SetParent(root, false);
            fallObject.GetComponent<MeshRenderer>().sharedMaterial = fallMaterial != null ? fallMaterial : streamMaterial;
            return fallObject.AddComponent<OutletFall>();
        }

        private PourStream CreateStream(Transform root)
        {
            var streamObject = new GameObject("Pour stream", typeof(LineRenderer));
            streamObject.transform.SetParent(root, false);
            var line = streamObject.GetComponent<LineRenderer>();
            line.sharedMaterial = streamMaterial;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.textureMode = LineTextureMode.Stretch;
            return streamObject.AddComponent<PourStream>();
        }
    }
}
