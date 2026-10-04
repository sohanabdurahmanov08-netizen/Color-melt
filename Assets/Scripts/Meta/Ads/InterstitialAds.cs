using System;
using UnityEngine;

namespace ColorMelt.Meta
{
    /// <summary>
    /// Interstitials between levels. Called when the player leaves a finished
    /// level; whether an ad plays depends on the pacing in Resources/AdsConfig
    /// (every few levels, or after a level once the timer has run out).
    /// </summary>
    public static class InterstitialAds
    {
        private const string Placement = "level_end";

        /// <summary>
        /// Shows an interstitial if the pacing allows it, then runs next.
        /// Never blocks the game: with no ad ready, next runs at once.
        /// </summary>
        public static void AfterLevel(int levelIndex, bool won, Action next)
        {
            if (ShouldShow(levelIndex, won))
                AdService.ShowInterstitial(Placement, next);
            else
                next?.Invoke();
        }

        private static bool ShouldShow(int levelIndex, bool won)
        {
            var config = AdsConfig.Instance;
            if (config.pacing == InterstitialPacing.Off) return false;
            if (!won && !config.afterLostLevels) return false;

            AdService.LevelsSinceAd++;
            if (levelIndex + 1 < config.firstLevelWithAds)
            {
                Debug.Log($"Ads: no interstitial before level {config.firstLevelWithAds}.");
                return false;
            }

            if (config.pacing == InterstitialPacing.EveryFewLevels)
            {
                Debug.Log($"Ads: {AdService.LevelsSinceAd}/{config.levelsBetweenAds} levels since the last ad.");
                return AdService.LevelsSinceAd >= config.levelsBetweenAds;
            }

            Debug.Log($"Ads: {AdService.SecondsSinceAd:F0}/{config.secondsBetweenAds:F0} s since the last ad.");
            return AdService.SecondsSinceAd >= config.secondsBetweenAds;
        }
    }
}
