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
        [SerializeField] private Vector3 routeSpacing = new Vector3(0f, 0f, 1.2f);
        [SerializeField] private Vector3 segmentSpacing = new Vector3(10f, 0f, 0f);
        [SerializeField] private Vector3 cubeScale = Vector3.one;
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

        [ContextMenu("Generate Missing Level 1 Routes (Safe)")]
        public void Generate()
        {
            if (colorRotePrefab == null)
            {
                Debug.LogError("Level1RouteGenerator: assign the Color_rote prefab.", this);
                return;
            }

            // Generation is additive. Designers can append a route in the
            // Inspector without losing placed levers, materials or manually
            // adjusted transforms on the routes that already exist.
            var root = transform.Find(GeneratedRootName);
            if (root == null)
            {
                root = new GameObject(GeneratedRootName).transform;
                root.SetParent(transform, false);
            }

            DisableNestedFlowManagers(root);

            var existingRouteCount = CountRouteRoots(root);
            for (var routeIndex = existingRouteCount; routeIndex < routes.Count; routeIndex++)
            {
                var route = routes[routeIndex];
                if (route == null || route.segmentCount < 1) continue;
                CreateRoute(root, routeIndex, route);
            }

            // This reconnects every route and ensures each Source, including
            // existing ones, has its cube lever immediately.
            RebuildExistingRoutes();
        }

        /// <summary>
        /// Restores source/channel/block links for routes saved in the scene.
        /// This preserves their meshes, transforms and existing components.
        /// </summary>
        [ContextMenu("Refresh Existing Routes And Levers")]
        public void RebuildExistingRoutes()
        {
            var root = transform.Find(GeneratedRootName);
            if (root == null) return;

            DisableNestedFlowManagers(root);

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

        private int CountRouteRoots(Transform root)
        {
            var count = 0;
            for (var childIndex = 0; childIndex < root.childCount; childIndex++)
                if (root.GetChild(childIndex).GetComponent<FutureSwitchAnchor>() == null)
                    count++;
            return count;
        }

        /// <summary>Creates one new route without touching any existing route.</summary>
        private void CreateRoute(Transform root, int routeIndex, RouteDefinition route)
        {
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

            if (previous == null) return;

            var sourceObject = new GameObject("Source");
            sourceObject.transform.SetParent(routeRoot, false);
            sourceObject.transform.position = routeStart - segmentSpacing;
            var source = sourceObject.AddComponent<SourceNode>();
            source.Configure(route.flowColor, routeChannels[0]);

            if (!route.generateBlock || blockChannel == null) return;

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
                var leverYaws = new List<float> { 0f };
                if (routeIndex > 0)
                {
                    destinations.Add(generatedRoutes[routeIndex - 1].firstChannel);
                    leverYaws.Add(-45f);
                }
                if (routeIndex < generatedRoutes.Count - 1)
                {
                    destinations.Add(generatedRoutes[routeIndex + 1].firstChannel);
                    leverYaws.Add(45f);
                }

                switchNode.ConfigureRuntimePositions(destinations);
                route.source.Configure(route.flowColor, switchNode);
                switches.Add(switchNode);

                // Every hill has a controllable source, so it always receives
                // a cube lever. This also repairs scenes made while lever
                // generation was an optional checkbox.
                CreateOrConfigureCubeLever(route, switchNode, leverYaws.ToArray());
            }
        }

        private void DisableNestedFlowManagers(Transform root)
        {
            if (flowManager == null)
                flowManager = GetComponent<LevelFlowManager>();

            foreach (var manager in root.GetComponentsInChildren<LevelFlowManager>(true))
            {
                if (manager != null && manager != flowManager)
                    manager.enabled = false;
            }
        }

        private void CreateOrConfigureCubeLever(GeneratedRoute route, SwitchNode switchNode, float[] positionYaws)
        {
            const string LeverName = "Lever cube";
            // Reuse the previous temporary triangle object if it is already in
            // a saved scene. Its transform/material remain untouched.
            var leverTransform = route.source.transform.Find(LeverName)
                ?? route.source.transform.Find("Triangle lever");
            if (leverTransform == null)
            {
                leverTransform = new GameObject(LeverName).transform;
                leverTransform.SetParent(route.source.transform, false);
                leverTransform.position = route.firstChannel.transform.position - segmentSpacing * 0.42f + Vector3.up * 0.6f;
            }

            var leverObject = leverTransform.gameObject;
            leverObject.name = LeverName;
            // Do not use ?? with Unity components here. A component that was
            // removed in the editor can be a managed non-null reference while
            // Unity considers it destroyed, which caused the previous error.
            var filter = leverObject.GetComponent<MeshFilter>();
            if (filter == null)
                filter = leverObject.AddComponent<MeshFilter>();
            filter.sharedMesh = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
            var renderer = leverObject.GetComponent<MeshRenderer>();
            if (renderer == null)
                renderer = leverObject.AddComponent<MeshRenderer>();
            var leverColour = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(leverColour);
            var sourceColour = ChannelVisual.ToUnityColor(route.flowColor);
            leverColour.SetColor("_BaseColor", sourceColour);
            leverColour.SetColor("_Color", sourceColour);
            renderer.SetPropertyBlock(leverColour);
            var meshCollider = leverObject.GetComponent<MeshCollider>();
            if (meshCollider != null)
                DestroyComponent(meshCollider);
            var boxCollider = leverObject.GetComponent<BoxCollider>();
            if (boxCollider == null)
                leverObject.AddComponent<BoxCollider>();
            var leverScript = leverObject.GetComponent<RouteSwitchLever>();
            if (leverScript == null)
                leverObject.AddComponent<RouteSwitchLever>();

            var lever = leverTransform.GetComponent<RouteSwitchLever>();
            if (lever != null)
                lever.Configure(flowManager, switchNode, positionYaws,
                    ChannelVisual.ToUnityColor(route.flowColor));
        }

        private static void DestroyComponent(Component component)
        {
            if (Application.isPlaying)
                Destroy(component);
            else
                DestroyImmediate(component);
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
