using System;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ColorMelt.Meta
{
    /// <summary>An ad backend: LevelPlay on phones, a simulated test ad elsewhere.</summary>
    internal interface IAdProvider
    {
        bool IsRewardedReady { get; }
        bool IsInterstitialReady { get; }

        /// <summary>
        /// onRewarded fires when the player has earned the reward (it may come
        /// just before or after the ad closes); onClosed fires exactly once.
        /// </summary>
        void ShowRewarded(string placement, Action onRewarded, Action onClosed);

        /// <summary>onClosed fires exactly once, also when the ad fails to show.</summary>
        void ShowInterstitial(string placement, Action onClosed);
    }

    /// <summary>
    /// Owns the ad backend and the shared state behind RewardedAds and
    /// InterstitialAds: one ad at a time, game audio paused while it plays,
    /// and the time and levels since the last ad for interstitial pacing.
    /// </summary>
    internal static class AdService
    {
        private static IAdProvider _provider;
        private static bool _showing;
        private static float _lastAdTime;

        /// <summary>Finished levels since the last ad of any kind.</summary>
        public static int LevelsSinceAd { get; set; }

        public static float SecondsSinceAd => Time.realtimeSinceStartup - _lastAdTime;

        public static bool IsRewardedReady => !_showing && _provider != null && _provider.IsRewardedReady;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            _showing = false;
            _lastAdTime = Time.realtimeSinceStartup;
            LevelsSinceAd = 0;

            var root = new GameObject("Ads");
            Object.DontDestroyOnLoad(root);
#if UNITY_ANDROID || UNITY_IOS
            _provider = root.AddComponent<LevelPlayAdProvider>();
#else
            // LevelPlay runs only on Android and iOS targets.
            _provider = root.AddComponent<TestAdProvider>();
#endif
        }

        public static void ShowRewarded(string placement, Action onRewarded, Action onFailed)
        {
            if (!IsRewardedReady)
            {
                Debug.Log($"Ads: rewarded '{placement}' is not ready.");
                onFailed?.Invoke();
                return;
            }

            var rewarded = false;
            Begin();
            _provider.ShowRewarded(placement,
                () =>
                {
                    if (rewarded) return;
                    rewarded = true;
                    Debug.Log($"Ads: rewarded '{placement}' earned.");
                    onRewarded?.Invoke();
                },
                () =>
                {
                    End();
                    if (!rewarded) onFailed?.Invoke();
                });
        }

        public static void ShowInterstitial(string placement, Action onDone)
        {
            if (_showing || _provider == null || !_provider.IsInterstitialReady)
            {
                Debug.Log($"Ads: interstitial '{placement}' is not ready, skipped.");
                onDone?.Invoke();
                return;
            }

            Begin();
            _provider.ShowInterstitial(placement, () =>
            {
                End();
                onDone?.Invoke();
            });
        }

        private static void Begin()
        {
            _showing = true;
            AudioListener.pause = true;
        }

        private static void End()
        {
            _showing = false;
            AudioListener.pause = false;
            // Any ad restarts the pacing, so a rewarded ad is never followed
            // straight away by an interstitial.
            _lastAdTime = Time.realtimeSinceStartup;
            LevelsSinceAd = 0;
        }
    }
}
