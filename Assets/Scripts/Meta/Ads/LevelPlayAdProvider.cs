#if UNITY_ANDROID || UNITY_IOS
using System;
using System.Collections;
using Unity.Services.LevelPlay;
using UnityEngine;

namespace ColorMelt.Meta
{
    /// <summary>
    /// Rewarded and interstitial ads through LevelPlay (Unity Ads Mediation).
    /// Keys come from Resources/AdsConfig. Both ads are loaded right after
    /// initialization and again after each show, so one is usually ready at
    /// the end of a level. In the Editor (with an Android or iOS target) the
    /// SDK shows its own mock ads.
    /// </summary>
    internal class LevelPlayAdProvider : MonoBehaviour, IAdProvider
    {
        // A reward can arrive just after the close event; wait this long for it.
        private const float RewardGraceSeconds = 1f;

        private LevelPlayRewardedAd _rewarded;
        private LevelPlayInterstitialAd _interstitial;
        private int _initAttempts;
        private int _rewardedAttempts;
        private int _interstitialAttempts;

        private Action _onRewarded;
        private Action _onRewardedClosed;
        private bool _rewardArrived;
        private Action _onInterstitialClosed;

        public bool IsRewardedReady => _rewarded != null && _rewarded.IsAdReady();
        public bool IsInterstitialReady => _interstitial != null && _interstitial.IsAdReady();

        private void Start()
        {
            var config = AdsConfig.Instance;
            LevelPlay.OnInitSuccess += OnInitSuccess;
            LevelPlay.OnInitFailed += OnInitFailed;
#if UNITY_IOS
            // Pause Unity while a full-screen ad is on top.
            LevelPlay.SetPauseGame(true);
#endif
            if (config.launchTestSuite && Debug.isDebugBuild)
                LevelPlay.SetMetaData("is_test_suite", "enable");

            Init();
        }

        private void Init()
        {
            Debug.Log($"Ads: LevelPlay init, app key {AdsConfig.Instance.AppKey}.");
            LevelPlay.Init(AdsConfig.Instance.AppKey);
        }

        private void OnInitSuccess(LevelPlayConfiguration configuration)
        {
            Debug.Log("Ads: LevelPlay SDK initialized successfully.");
            if (AdsConfig.Instance.launchTestSuite && Debug.isDebugBuild)
                LevelPlay.LaunchTestSuite();

            CreateRewarded();
            CreateInterstitial();
        }

        private void OnInitFailed(LevelPlayInitError error)
        {
            Debug.LogWarning($"Ads: LevelPlay init failed ({error.ErrorCode}): {error.ErrorMessage}");
            StartCoroutine(After(RetryDelay(_initAttempts++), Init));
        }

        private void OnDestroy()
        {
            LevelPlay.OnInitSuccess -= OnInitSuccess;
            LevelPlay.OnInitFailed -= OnInitFailed;

            if (_rewarded != null)
            {
                _rewarded.OnAdLoaded -= OnRewardedLoaded;
                _rewarded.OnAdLoadFailed -= OnRewardedLoadFailed;
                _rewarded.OnAdDisplayed -= OnRewardedDisplayed;
                _rewarded.OnAdDisplayFailed -= OnRewardedDisplayFailed;
                _rewarded.OnAdRewarded -= OnRewardedEarned;
                _rewarded.OnAdClosed -= OnRewardedClosed;
                _rewarded.OnAdClicked -= OnAdClicked;
                _rewarded.DestroyAd();
                _rewarded = null;
            }

            if (_interstitial != null)
            {
                _interstitial.OnAdLoaded -= OnInterstitialLoaded;
                _interstitial.OnAdLoadFailed -= OnInterstitialLoadFailed;
                _interstitial.OnAdDisplayed -= OnInterstitialDisplayed;
                _interstitial.OnAdDisplayFailed -= OnInterstitialDisplayFailed;
                _interstitial.OnAdClosed -= OnInterstitialClosed;
                _interstitial.OnAdClicked -= OnAdClicked;
                _interstitial.DestroyAd();
                _interstitial = null;
            }
        }

        // ---- Rewarded ----

        private void CreateRewarded()
        {
            if (_rewarded != null) return;
            _rewarded = new LevelPlayRewardedAd(AdsConfig.Instance.RewardedId);
            _rewarded.OnAdLoaded += OnRewardedLoaded;
            _rewarded.OnAdLoadFailed += OnRewardedLoadFailed;
            _rewarded.OnAdDisplayed += OnRewardedDisplayed;
            _rewarded.OnAdDisplayFailed += OnRewardedDisplayFailed;
            _rewarded.OnAdRewarded += OnRewardedEarned;
            _rewarded.OnAdClosed += OnRewardedClosed;
            _rewarded.OnAdClicked += OnAdClicked;
            LoadRewarded();
        }

        private void LoadRewarded() => _rewarded?.LoadAd();

        public void ShowRewarded(string placement, Action onRewarded, Action onClosed)
        {
            _onRewarded = onRewarded;
            _onRewardedClosed = onClosed;
            _rewardArrived = false;
            Debug.Log($"Ads: showing rewarded '{placement}'.");
            // Placement names are not passed on: they must exist in the
            // LevelPlay dashboard, and the demo app has none.
            _rewarded.ShowAd();
        }

        private void OnRewardedLoaded(LevelPlayAdInfo info)
        {
            _rewardedAttempts = 0;
            Debug.Log($"Ads: rewarded loaded ({info.AdNetwork}).");
        }

        private void OnRewardedLoadFailed(LevelPlayAdError error)
        {
            Debug.LogWarning($"Ads: rewarded failed to load ({error.ErrorCode}): {error.ErrorMessage}");
            StartCoroutine(After(RetryDelay(_rewardedAttempts++), LoadRewarded));
        }

        private void OnRewardedDisplayed(LevelPlayAdInfo info) => Debug.Log("Ads: rewarded displayed.");

        private void OnRewardedDisplayFailed(LevelPlayAdInfo info, LevelPlayAdError error)
        {
            Debug.LogWarning($"Ads: rewarded failed to display ({error.ErrorCode}): {error.ErrorMessage}");
            FinishRewarded();
            LoadRewarded();
        }

        private void OnRewardedEarned(LevelPlayAdInfo info, LevelPlayReward reward)
        {
            _rewardArrived = true;
            _onRewarded?.Invoke();
        }

        private void OnRewardedClosed(LevelPlayAdInfo info)
        {
            Debug.Log("Ads: rewarded closed.");
            StartCoroutine(CloseRewardedRoutine());
            LoadRewarded();
        }

        private IEnumerator CloseRewardedRoutine()
        {
            for (var time = 0f; !_rewardArrived && time < RewardGraceSeconds; time += Time.unscaledDeltaTime)
                yield return null;
            FinishRewarded();
        }

        private void FinishRewarded()
        {
            var closed = _onRewardedClosed;
            _onRewardedClosed = null;
            closed?.Invoke();
        }

        // ---- Interstitial ----

        private void CreateInterstitial()
        {
            if (_interstitial != null) return;
            _interstitial = new LevelPlayInterstitialAd(AdsConfig.Instance.InterstitialId);
            _interstitial.OnAdLoaded += OnInterstitialLoaded;
            _interstitial.OnAdLoadFailed += OnInterstitialLoadFailed;
            _interstitial.OnAdDisplayed += OnInterstitialDisplayed;
            _interstitial.OnAdDisplayFailed += OnInterstitialDisplayFailed;
            _interstitial.OnAdClosed += OnInterstitialClosed;
            _interstitial.OnAdClicked += OnAdClicked;
            LoadInterstitial();
        }

        private void LoadInterstitial() => _interstitial?.LoadAd();

        public void ShowInterstitial(string placement, Action onClosed)
        {
            _onInterstitialClosed = onClosed;
            Debug.Log($"Ads: showing interstitial '{placement}'.");
            _interstitial.ShowAd();
        }

        private void OnInterstitialLoaded(LevelPlayAdInfo info)
        {
            _interstitialAttempts = 0;
            Debug.Log($"Ads: interstitial loaded ({info.AdNetwork}).");
        }

        private void OnInterstitialLoadFailed(LevelPlayAdError error)
        {
            Debug.LogWarning($"Ads: interstitial failed to load ({error.ErrorCode}): {error.ErrorMessage}");
            StartCoroutine(After(RetryDelay(_interstitialAttempts++), LoadInterstitial));
        }

        private void OnInterstitialDisplayed(LevelPlayAdInfo info) => Debug.Log("Ads: interstitial displayed.");

        private void OnInterstitialDisplayFailed(LevelPlayAdInfo info, LevelPlayAdError error)
        {
            Debug.LogWarning($"Ads: interstitial failed to display ({error.ErrorCode}): {error.ErrorMessage}");
            FinishInterstitial();
            LoadInterstitial();
        }

        private void OnInterstitialClosed(LevelPlayAdInfo info)
        {
            Debug.Log("Ads: interstitial closed.");
            FinishInterstitial();
            LoadInterstitial();
        }

        private void FinishInterstitial()
        {
            var closed = _onInterstitialClosed;
            _onInterstitialClosed = null;
            closed?.Invoke();
        }

        // ---- Shared ----

        private static void OnAdClicked(LevelPlayAdInfo info) => Debug.Log($"Ads: {info.AdFormat} clicked.");

        /// <summary>5, 10, 20, 40, then 60 seconds between retries.</summary>
        private static float RetryDelay(int attempt) => Mathf.Min(60f, 5f * (1 << Mathf.Min(attempt, 4)));

        private static IEnumerator After(float seconds, Action action)
        {
            yield return new WaitForSecondsRealtime(seconds);
            action();
        }
    }
}
#endif
