using System;
using System.Collections.Generic;
using UnityEngine;

namespace ColorMelt.Core
{
    /// <summary>
    /// One Color Melt level. Routes are laid out left to right; each route's
    /// source can pour into its own channel or into a neighbouring one.
    /// Blocks sit on a channel and melt when the paint reaching them has
    /// exactly their colour. Create via Assets > Create > Color Melt > Level
    /// and add the asset to the LevelDatabase.
    /// </summary>
    [CreateAssetMenu(menuName = "Color Melt/Level", fileName = "Level_00")]
    public class LevelData : ScriptableObject
    {
        public const float MinBlockPosition = 0.5f;
        public const float MaxBlockPosition = 0.95f;

        [Tooltip("Moves the player has. The editor shows the optimal solution length.")]
        [Min(1)] public int maxMoves = 3;

        [Tooltip("Walk the player through the optimal solution step by step.")]
        public bool tutorial;

        public List<RouteData> routes = new List<RouteData>();

        [Tooltip("Filled in by the level editor's solver. Used for star rating and hints.")]
        public int optimalMoves = -1;

        public int BlockCount
        {
            get
            {
                var count = 0;
                foreach (var route in routes)
                    count += route.blocks.Count;
                return count;
            }
        }

        private void OnValidate()
        {
            foreach (var route in routes)
            {
                foreach (var block in route.blocks)
                    block.position = Mathf.Clamp(block.position, MinBlockPosition, MaxBlockPosition);

                // Flow reaches blocks from the top of the channel down.
                route.blocks.Sort((a, b) => a.position.CompareTo(b.position));
            }
        }
    }

    [Serializable]
    public class RouteData
    {
        public ColorType color = ColorType.Red;
        public List<BlockData> blocks = new List<BlockData>();
    }

    [Serializable]
    public class BlockData
    {
        public ColorType color = ColorType.Purple;

        [Tooltip("Position along the channel: 0 = top inlet, 1 = bottom outlet.")]
        [Range(LevelData.MinBlockPosition, LevelData.MaxBlockPosition)]
        public float position = 0.85f;
    }
}
