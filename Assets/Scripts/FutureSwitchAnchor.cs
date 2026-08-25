using UnityEngine;

namespace ColorMelt.Core
{
    /// <summary>
    /// Placeholder for a future triangular lever. It marks the exact position
    /// between two generated routes and preserves their possible destinations.
    /// A SwitchNode and triangle visual can later be added here without
    /// changing Level1RouteGenerator or rebuilding the level layout.
    /// </summary>
    [DisallowMultipleComponent]
    public class FutureSwitchAnchor : MonoBehaviour
    {
        [SerializeField] private ChannelNode firstRoute;
        [SerializeField] private ChannelNode secondRoute;

        public ChannelNode FirstRoute => firstRoute;
        public ChannelNode SecondRoute => secondRoute;

        public void Configure(ChannelNode first, ChannelNode second)
        {
            firstRoute = first;
            secondRoute = second;
        }
    }
}
