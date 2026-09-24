using System;
using UnityEngine;

namespace ColorMelt.Meta
{
    /// <summary>
    /// Single entry point for rewarded ads. Until an ad SDK (e.g. Unity
    /// LevelPlay) is integrated, the reward is granted immediately so the
    /// hybrid-casual loops (x2 reward, free coins, continue) can be played
    /// and balanced. Replace the body of Show with the SDK call.
    /// </summary>
    public static class RewardedAds
    {
        public static bool IsReady => true;

        public static void Show(string placement, Action onRewarded, Action onFailed = null)
        {
            Debug.Log($"RewardedAds: '{placement}' shown (stub, reward granted).");
            onRewarded?.Invoke();
        }
    }

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
