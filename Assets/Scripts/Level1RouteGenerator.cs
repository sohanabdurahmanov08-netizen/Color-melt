using System;
using System.Collections.Generic;
using UnityEngine;

namespace ColorMelt.Core
{
    [Serializable]
    public class RouteDefinition
    {
        public string name = "New route";
        public ColorType flowColor = ColorType.Red;
        [Min(1)] public int segmentCount = 1;

        [Header("Optional cube block")]
        public bool generateBlock;
        public ColorType blockRequiredColor = ColorType.Red;
        [Tooltip("-1 means the final segment of this route.")]
        public int blockSegmentIndex = -1;
        [Range(0f, 1f)] public float blockStartNormalized = 0.8f;
    }

    /// <summary>
    /// Level 1's editable route factory. Each entry in Routes produces a row:
    /// source -> one or more Color_rote segments -> optional coloured cube block.
    /// Routes are deliberately independent until switches are added later.
    /// </summary>
    public class Level1RouteGenerator : MonoBehaviour
    {
        [Header("Required references")]
        [SerializeField] private GameObject colorRotePrefab;
        [SerializeField] private LevelFlowManager flowManager;

        [Header("Layout")]
        [SerializeField] private Vector3 firstRoutePosition = Vector3.zero;
        [SerializeField] private Vector3 routeSpacing = new Vector3(0f, 0f, 3.5f);
        [SerializeField] private Vector3 segmentSpacing = new Vector3(10f, 0f, 0f);
        [SerializeField] private Vector3 cubeScale = Vector3.one;
        [SerializeField] private bool generateTriangleLevers = true;
        [SerializeField] private bool prepareSwitchAnchors = true;
        [SerializeField] private bool generateOnAwake;

        [Header("Routes")]
        [SerializeField] private List<RouteDefinition> routes = new List<RouteDefinition>();

        private const string GeneratedRootName = "__Generated_Level1_Routes";

        private void Awake()
        {
            if (!Application.isPlaying) return;

            // Runtime links are deliberately not serialized into prefab/scene
            // YAML. Rebuild them after every load so an already assembled level
            // keeps working without being destroyed and generated again.
            if (generateOnAwake)
                Generate();
            else
                RebuildExistingRoutes();
        }

        private void Reset()
        {
            flowManager = GetComponent<LevelFlowManager>();
            routes = new List<RouteDefinition>
            {
                new RouteDefinition { name = "Red route", flowColor = ColorType.Red, generateBlock = true, blockRequiredColor = ColorType.Blue },
                new RouteDefinition { name = "Blue route", flowColor = ColorType.Blue },
                new RouteDefinition { name = "Yellow route", flowColor = ColorType.Yellow, generateBlock = true, blockRequiredColor = ColorType.Yellow }
            };
        }

        [ContextMenu("Generate Level 1 Routes")]
        public void Generate()
        {
            if (colorRotePrefab == null)
            {
                Debug.LogError("Level1RouteGenerator: assign the Color_rote prefab.", this);
                return;
            }

            ClearGenerated();

            var root = new GameObject(GeneratedRootName).transform;
            root.SetParent(transform, false);
            var sources = new List<SourceNode>();
            var blocks = new List<BlockNode>();
            var switches = new List<SwitchNode>();
            var channels = new List<ChannelNode>();
            var generatedRoutes = new List<GeneratedRoute>();

            for (var routeIndex = 0; routeIndex < routes.Count; routeIndex++)
            {
                var route = routes[routeIndex];
                if (route == null || route.segmentCount < 1) continue;

                var routeRoot = new GameObject(string.IsNullOrWhiteSpace(route.name)
                    ? $"Route {routeIndex + 1}" : route.name).transform;
                routeRoot.SetParent(root, false);

                var routeStart = firstRoutePosition + routeSpacing * routeIndex;
                ChannelNode previous = null;
                ChannelNode blockChannel = null;
                ChannelVisual blockVisual = null;
                var routeChannels = new List<ChannelNode>();

                for (var segmentIndex = 0; segmentIndex < route.segmentCount; segmentIndex++)
                {
                    var segment = Instantiate(colorRotePrefab, routeStart + segmentSpacing * segmentIndex,
                        colorRotePrefab.transform.rotation, routeRoot);
                    segment.name = $"Color_rote {segmentIndex + 1}";

                    var channel = segment.GetComponent<ChannelNode>();
                    if (channel == null)
                    {
                        Debug.LogError("Level1RouteGenerator: Color_rote prefab needs ChannelNode on its root.", segment);
                        DestroyObject(segment);
                        continue;
                    }

                    if (previous != null)
                        previous.SetRuntimeOutput(channel);
                    previous = channel;
                    channels.Add(channel);
                    routeChannels.Add(channel);
                    segment.GetComponent<ChannelVisual>()?.SetConfiguredFlowColor(route.flowColor);

                    var requestedBlockSegment = route.blockSegmentIndex < 0
                        ? route.segmentCount - 1
                        : Mathf.Clamp(route.blockSegmentIndex, 0, route.segmentCount - 1);
                    if (segmentIndex == requestedBlockSegment)
                    {
                        blockChannel = channel;
                        blockVisual = segment.GetComponent<ChannelVisual>();
                    }
                }

                if (previous == null) continue;

                var sourceObject = new GameObject("Source");
                sourceObject.transform.SetParent(routeRoot, false);
                sourceObject.transform.position = routeStart - segmentSpacing;
                var source = sourceObject.AddComponent<SourceNode>();
                var firstChannel = GetFirstChannel(routeRoot);
                source.Configure(route.flowColor, firstChannel);
                sources.Add(source);
                generatedRoutes.Add(new GeneratedRoute(firstChannel, routeStart, source, route.flowColor));

                if (route.generateBlock && blockChannel != null)
                {
                    var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    cube.name = "Color block";
                    cube.transform.SetParent(routeRoot, true);
                    var blockSegment = route.blockSegmentIndex < 0 ? route.segmentCount - 1
                        : Mathf.Clamp(route.blockSegmentIndex, 0, route.segmentCount - 1);
                    cube.transform.position = routeStart + segmentSpacing * (blockSegment + route.blockStartNormalized);
                    cube.transform.localScale = cubeScale;
                    SetCubeColor(cube, route.blockRequiredColor);

                    var block = cube.AddComponent<BlockNode>();
                    var nodeAfterBlock = blockSegment + 1 < routeChannels.Count
                        ? routeChannels[blockSegment + 1]
                        : null;
                    block.Configure(route.blockRequiredColor, blockVisual, route.blockStartNormalized, cube, nodeAfterBlock);
                    blockChannel.SetRuntimeOutput(block);
                    blocks.Add(block);
                }
            }

            ConfigureRouteSwitches(generatedRoutes, switches);

            if (prepareSwitchAnchors)
                CreateSwitchAnchors(root, generatedRoutes);

            if (flowManager == null)
                flowManager = GetComponent<LevelFlowManager>();

            if (flowManager != null)
                flowManager.ConfigureGeneratedGraph(sources, switches, blocks, channels);
            else
                Debug.LogWarning("Level1RouteGenerator: no LevelFlowManager assigned; routes are visual only.", this);
        }

        /// <summary>
        /// Restores source/channel/block links for routes saved in the scene.
        /// This preserves their meshes, transforms and existing components.
        /// </summary>
        public void RebuildExistingRoutes()
        {
            var root = transform.Find(GeneratedRootName);
            if (root == null) return;

            var sources = new List<SourceNode>();
            var blocks = new List<BlockNode>();
            var switches = new List<SwitchNode>();
            var allChannels = new List<ChannelNode>();
            var generatedRoutes = new List<GeneratedRoute>();

            for (var routeIndex = 0; routeIndex < root.childCount; routeIndex++)
            {
                var routeRoot = root.GetChild(routeIndex);
                if (routeRoot.GetComponent<FutureSwitchAnchor>() != null) continue;

                var channels = routeRoot.GetComponentsInChildren<ChannelNode>(true);
                var source = routeRoot.GetComponentInChildren<SourceNode>(true);
                if (channels.Length == 0 || source == null) continue;

                var definition = routeIndex < routes.Count ? routes[routeIndex] : null;
                var flowColor = definition != null ? definition.flowColor : source.CurrentColor;
                source.Configure(flowColor, channels[0]);
                sources.Add(source);
                generatedRoutes.Add(new GeneratedRoute(channels[0], channels[0].transform.position, source, flowColor));
                allChannels.AddRange(channels);

                for (var channelIndex = 0; channelIndex < channels.Length - 1; channelIndex++)
                {
                    channels[channelIndex].SetRuntimeOutput(channels[channelIndex + 1]);
                    channels[channelIndex].GetComponent<ChannelVisual>()?.SetConfiguredFlowColor(flowColor);
                }
                channels[channels.Length - 1].GetComponent<ChannelVisual>()?.SetConfiguredFlowColor(flowColor);

                var block = routeRoot.GetComponentInChildren<BlockNode>(true);
                if (block != null)
                {
                    var blockIndex = definition == null || definition.blockSegmentIndex < 0
                        ? channels.Length - 1
                        : Mathf.Clamp(definition.blockSegmentIndex, 0, channels.Length - 1);
                    var requiredColor = definition != null ? definition.blockRequiredColor : block.RequiredColor;
                    var normalizedStart = definition != null ? definition.blockStartNormalized : block.BlockStartNormalized;
                    var nodeAfterBlock = blockIndex + 1 < channels.Length ? channels[blockIndex + 1] : null;
                    block.Configure(requiredColor, channels[blockIndex].GetComponent<ChannelVisual>(),
                        normalizedStart, block.gameObject, nodeAfterBlock);
                    channels[blockIndex].SetRuntimeOutput(block);
                    blocks.Add(block);
                }
            }

            ConfigureRouteSwitches(generatedRoutes, switches);

            if (flowManager == null)
                flowManager = GetComponent<LevelFlowManager>();

            if (flowManager != null)
                flowManager.ConfigureGeneratedGraph(sources, switches, blocks, allChannels);

            if (prepareSwitchAnchors && generatedRoutes.Count > 1)
                RebuildSwitchAnchors(root, generatedRoutes);
        }

        [ContextMenu("Clear Generated Level 1 Routes")]
        public void ClearGenerated()
        {
            var oldRoot = transform.Find(GeneratedRootName);
            if (oldRoot != null)
                DestroyObject(oldRoot.gameObject);
        }

        private static ChannelNode GetFirstChannel(Transform routeRoot)
        {
            return routeRoot.GetComponentInChildren<ChannelNode>();
        }

        private void CreateSwitchAnchors(Transform root, List<GeneratedRoute> generatedRoutes)
        {
            for (var routeIndex = 0; routeIndex < generatedRoutes.Count - 1; routeIndex++)
            {
                var upperRoute = generatedRoutes[routeIndex];
                var lowerRoute = generatedRoutes[routeIndex + 1];
                var anchorObject = new GameObject($"Switch anchor {routeIndex + 1}-{routeIndex + 2}");
                anchorObject.transform.SetParent(root, false);
                anchorObject.transform.position = (upperRoute.startPosition + lowerRoute.startPosition) * 0.5f
                    + segmentSpacing * 0.5f;
                anchorObject.AddComponent<FutureSwitchAnchor>()
                    .Configure(upperRoute.firstChannel, lowerRoute.firstChannel);
            }
        }

        private void ConfigureRouteSwitches(List<GeneratedRoute> generatedRoutes, List<SwitchNode> switches)
        {
            if (flowManager == null)
                flowManager = GetComponent<LevelFlowManager>();

            for (var routeIndex = 0; routeIndex < generatedRoutes.Count; routeIndex++)
            {
                var route = generatedRoutes[routeIndex];
                var switchNode = route.source.GetComponent<SwitchNode>();
                if (switchNode == null)
                    switchNode = route.source.gameObject.AddComponent<SwitchNode>();

                var destinations = new List<IFlowNode> { route.firstChannel };
                if (routeIndex > 0)
                    destinations.Add(generatedRoutes[routeIndex - 1].firstChannel);
                if (routeIndex < generatedRoutes.Count - 1)
                    destinations.Add(generatedRoutes[routeIndex + 1].firstChannel);

                switchNode.ConfigureRuntimePositions(destinations);
                route.source.Configure(route.flowColor, switchNode);
                switches.Add(switchNode);

                if (generateTriangleLevers)
                    CreateOrConfigureTriangleLever(route, switchNode);
            }
        }

        private void CreateOrConfigureTriangleLever(GeneratedRoute route, SwitchNode switchNode)
        {
            const string LeverName = "Triangle lever";
            var leverTransform = route.source.transform.Find(LeverName);
            if (leverTransform == null)
            {
                leverTransform = new GameObject(LeverName).transform;
                leverTransform.SetParent(route.source.transform, false);
                leverTransform.position = route.firstChannel.transform.position - segmentSpacing * 0.42f + Vector3.up * 0.6f;
            }

            var leverObject = leverTransform.gameObject;
            var filter = leverObject.GetComponent<MeshFilter>() ?? leverObject.AddComponent<MeshFilter>();
            if (filter.sharedMesh == null)
                filter.sharedMesh = CreateTriangleMesh();
            if (leverObject.GetComponent<MeshRenderer>() == null)
                leverObject.AddComponent<MeshRenderer>();
            var meshCollider = leverObject.GetComponent<MeshCollider>() ?? leverObject.AddComponent<MeshCollider>();
            meshCollider.sharedMesh = filter.sharedMesh;
            if (leverObject.GetComponent<RouteSwitchLever>() == null)
                leverObject.AddComponent<RouteSwitchLever>();

            var lever = leverTransform.GetComponent<RouteSwitchLever>();
            if (lever != null)
                lever.Configure(flowManager, switchNode);
        }

        private static Mesh CreateTriangleMesh()
        {
            var mesh = new Mesh { name = "Triangle lever mesh" };
            mesh.vertices = new[]
            {
                new Vector3(-0.45f, 0f, -0.12f),
                new Vector3(0.45f, 0f, -0.12f),
                new Vector3(0f, 0.8f, -0.12f),
                new Vector3(-0.45f, 0f, 0.12f),
                new Vector3(0.45f, 0f, 0.12f),
                new Vector3(0f, 0.8f, 0.12f)
            };
            mesh.triangles = new[]
            {
                0, 1, 2, 5, 4, 3, // front and back
                0, 3, 4, 0, 4, 1, // bottom
                1, 4, 5, 1, 5, 2, // right
                2, 5, 3, 2, 3, 0  // left
            };
            mesh.uv = new[]
            {
                Vector2.zero, Vector2.right, Vector2.up,
                Vector2.zero, Vector2.right, Vector2.up
            };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private void RebuildSwitchAnchors(Transform root, List<GeneratedRoute> generatedRoutes)
        {
            for (var routeIndex = 0; routeIndex < generatedRoutes.Count - 1; routeIndex++)
            {
                var anchor = root.Find($"Switch anchor {routeIndex + 1}-{routeIndex + 2}");
                if (anchor == null) continue;

                anchor.GetComponent<FutureSwitchAnchor>()?.Configure(
                    generatedRoutes[routeIndex].firstChannel, generatedRoutes[routeIndex + 1].firstChannel);
            }
        }

        private static void SetCubeColor(GameObject cube, ColorType colorType)
        {
            var renderer = cube.GetComponent<Renderer>();
            if (renderer == null) return;

            var block = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(block);
            var color = ChannelVisual.ToUnityColor(colorType);
            block.SetColor("_BaseColor", color);
            block.SetColor("_Color", color);
            renderer.SetPropertyBlock(block);
        }

        private static void DestroyObject(GameObject gameObject)
        {
            if (Application.isPlaying)
                Destroy(gameObject);
            else
                DestroyImmediate(gameObject);
        }

        private readonly struct GeneratedRoute
        {
            public readonly ChannelNode firstChannel;
            public readonly Vector3 startPosition;
            public readonly SourceNode source;
            public readonly ColorType flowColor;

            public GeneratedRoute(ChannelNode firstChannel, Vector3 startPosition, SourceNode source, ColorType flowColor)
            {
                this.firstChannel = firstChannel;
                this.startPosition = startPosition;
                this.source = source;
                this.flowColor = flowColor;
            }
        }
    }
}
