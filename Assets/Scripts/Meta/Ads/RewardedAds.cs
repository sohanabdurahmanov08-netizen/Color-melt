using System;

namespace ColorMelt.Meta
{
    /// <summary>
    /// Single entry point for rewarded ads (x2 reward, continue, free coins).
    /// On Android and iOS the ad comes from LevelPlay; in the Editor with a
    /// PC target a simulated test ad is shown instead (see AdService).
    /// </summary>
    public static class RewardedAds
    {
        /// <summary>True when an ad is loaded and nothing else is showing.</summary>
        public static bool IsReady => AdService.IsRewardedReady;

        /// <summary>
        /// Shows a rewarded ad. onRewarded runs once the player has earned the
        /// reward; onFailed runs when there was no ad or the player closed it
        /// early. The placement name is for logs.
        /// </summary>
        public static void Show(string placement, Action onRewarded, Action onFailed = null)
        {
            AdService.ShowRewarded(placement, onRewarded, onFailed);
        }
    }
}
