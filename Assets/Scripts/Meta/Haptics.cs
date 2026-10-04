using UnityEngine;

namespace ColorMelt.Meta
{
    /// <summary>
    /// Haptic feedback for big moments (melt, win). Handheld.Vibrate is a
    /// full buzz, so it is not used for ordinary taps. Respects settings.
    /// </summary>
    public static class Haptics
    {
        public static void Impact()
        {
#if UNITY_ANDROID || UNITY_IOS
            if (Progress.VibrationOn)
                Handheld.Vibrate();
#endif
        }
    }
}
