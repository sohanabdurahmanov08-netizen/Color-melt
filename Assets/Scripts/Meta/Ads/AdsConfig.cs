using UnityEngine;

namespace ColorMelt.Meta
{
    /// <summary>When an interstitial may follow a finished level.</summary>
    public enum InterstitialPacing
    {
        Off,
        /// <summary>After every few finished levels.</summary>
        EveryFewLevels,
        /// <summary>After a level, but only once the timer since the last ad has run out.</summary>
        Timer
    }

    /// <summary>
    /// Ad settings in one asset (Resources/AdsConfig): LevelPlay keys and how
    /// often interstitials appear. The default keys belong to LevelPlay's demo
    /// app from the Ads Mediation package sample, which only serves test ads.
    /// Replace them with your own app and ad units from the LevelPlay
    /// dashboard before release.
    /// </summary>
    [CreateAssetMenu(menuName = "Color Melt/Ads Config", fileName = "AdsConfig")]
    public class AdsConfig : ScriptableObject
    {
        private const string ResourcePath = "AdsConfig";

        [Header("LevelPlay: Android")]
        public string androidAppKey = "25b63cf85";
        public string androidRewardedId = "syz3d8ekts22q0or";
        public string androidInterstitialId = "h3xw38h9214adgxo";

        [Header("LevelPlay: iOS")]
        public string iosAppKey = "25c43a4a5";
        public string iosRewardedId = "l1quzz1xmmdhw5er";
        public string iosInterstitialId = "obg6ohwts3y690ks";

        [Header("Interstitials between levels")]
        public InterstitialPacing pacing = InterstitialPacing.Timer;
        [Tooltip("EveryFewLevels: an ad after this many finished levels.")]
        [Min(1)] public int levelsBetweenAds = 3;
        [Tooltip("Timer: an ad after a level only if this many seconds have passed since the last ad (or the game start).")]
        [Min(5f)] public float secondsBetweenAds = 60f;
        [Tooltip("No interstitials before the player finishes this level (1-based), so the first levels stay ad-free.")]
        [Min(1)] public int firstLevelWithAds = 3;
        [Tooltip("Also show interstitials when leaving a lost level (Retry or Menu after a defeat).")]
        public bool afterLostLevels;

        [Header("Testing")]
        [Tooltip("Development builds only: open the LevelPlay Test Suite on start to check every ad network on a device.")]
        public bool launchTestSuite;
        [Tooltip("Length of the simulated test ad used where LevelPlay does not run (the Editor with a PC target, desktop builds).")]
        [Min(1f)] public float testAdSeconds = 5f;

        private static AdsConfig _instance;

        public static AdsConfig Instance
        {
            get
            {
                if (_instance == null)
                    _instance = Resources.Load<AdsConfig>(ResourcePath);
                if (_instance == null)
                    _instance = CreateInstance<AdsConfig>();
                return _instance;
            }
        }

#if UNITY_IOS
        public string AppKey => iosAppKey;
        public string RewardedId => iosRewardedId;
        public string InterstitialId => iosInterstitialId;
#else
        public string AppKey => androidAppKey;
        public string RewardedId => androidRewardedId;
        public string InterstitialId => androidInterstitialId;
#endif
    }
}
