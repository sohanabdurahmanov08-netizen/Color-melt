using UnityEngine;

namespace ColorMelt.Meta
{
    /// <summary>Runtime settings for phones, applied once before the first scene loads.</summary>
    public static class MobileSettings
    {
        private const int TargetFrameRate = 60;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Apply()
        {
            // Phones default to 30 fps; pours, melts and the paint transition
            // are animated for 60.
            if (Application.isMobilePlatform)
                Application.targetFrameRate = TargetFrameRate;
        }
    }
}
