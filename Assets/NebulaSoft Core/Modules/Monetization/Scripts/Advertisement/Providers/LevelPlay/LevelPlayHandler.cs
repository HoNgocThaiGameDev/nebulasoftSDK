#pragma warning disable 0618

using System.Threading.Tasks;
using UnityEngine;

#if MODULE_LEVELPLAY
using Unity.Services.LevelPlay;
#endif

namespace NebulaSoft
{
#if MODULE_LEVELPLAY
    public class LevelPlayHandler : AdProviderHandler
    {
        private LevelPlayBannerAd bannerAd;
        private LevelPlayInterstitialAd interstitialAd;
        private LevelPlayRewardedAd rewardedVideoAd;

        protected TaskCompletionSource<bool> loadingTask;

        public LevelPlayHandler(AdProvider moduleType) : base(moduleType) { }

        protected override async Task<bool> InitProviderAsync()
        {
            if (Monetization.VerboseLogging)
            {
                Debug.Log("[AdsManager]: LevelPlay is trying to initialize!", adsSettings);

                LevelPlay.ValidateIntegration();
            }

            loadingTask = new TaskCompletionSource<bool>();

            LevelPlay.OnInitSuccess += OnInitSucces;
            LevelPlay.OnInitFailed += OnInitFailed;

            LevelPlay.SetConsent(ConsentData.IsConsentGiven);

            if (Monetization.DebugMode)
                LevelPlay.SetMetaData("is_test_suite", "enable");

            LevelPlay.Init(GetAppKey());

            return await loadingTask.Task;
        }

        private void OnInitFailed(LevelPlayInitError error)
        {
            Debug.LogError($"[AdsManager]: LevelPlay failed to initialized! Error: #{error.ErrorCode} {error.ErrorMessage}");

            loadingTask?.SetResult(false);
        }

        private void OnInitSucces(LevelPlayConfiguration configuration)
        {
            if (Monetization.VerboseLogging)
                Debug.Log("[AdsManager]: LevelPlay is initialized!");

            OnInitCompleted();

            loadingTask?.SetResult(true);

            LevelPlay.OnInitSuccess -= OnInitSucces;
            LevelPlay.OnInitFailed -= OnInitFailed;
        }


        private void OnInitCompleted()
        {
            // Keep the LevelPlay fallback on the smallest fixed standard banner.
            // Adaptive, large, MREC, and leaderboard formats would invalidate the
            // shared bottom-navigation layout contract.
            LevelPlayBannerAd.Config bannerConfig = new LevelPlayBannerAd.Config.Builder()
                .SetPosition(LevelPlayBannerPosition.BottomCenter)
                .SetSize(LevelPlayAdSize.BANNER)
                .Build();

            bannerAd = new LevelPlayBannerAd(GetBannerID(), bannerConfig);
            bannerAd.OnAdLoaded += BannerOnAdLoadedEvent;
            bannerAd.OnAdDisplayed += BannerOnAdDisplayedEvent;

            // Interstitial
            interstitialAd = new LevelPlayInterstitialAd(GetInterstitialID());

            // Register to events
            interstitialAd.OnAdLoaded += InterstitialOnAdLoadedEvent;
            interstitialAd.OnAdLoadFailed += InterstitialOnAdLoadFailedEvent;
            interstitialAd.OnAdDisplayed += InterstitialOnAdDisplayedEvent;
            interstitialAd.OnAdDisplayFailed += InterstitialOnAdDisplayFailedEvent;
            interstitialAd.OnAdClosed += InterstitialOnAdClosedEvent;

            // Create Rewarded Video object
            rewardedVideoAd = new LevelPlayRewardedAd(GetRewardedVideoID());

            // Register to Rewarded Video events
            rewardedVideoAd.OnAdDisplayed += RewardedVideoOnAdOpenedEvent;
            rewardedVideoAd.OnAdDisplayFailed += RewardedVideoOnAdShowFailedEvent;
            rewardedVideoAd.OnAdRewarded += RewardedVideoOnAdRewardedEvent;
            rewardedVideoAd.OnAdClosed += RewardedVideoOnAdClosedEvent;

            if (Monetization.DebugMode)
                OpenTestSuite();
        }

        #region Rewarded Ad
        public override void RequestRewardedVideo()
        {
            if (rewardedVideoAd != null)
                rewardedVideoAd.LoadAd();
        }

        public override void ShowRewardedVideo(AdvertisementCallback callback)
        {
            if (rewardedVideoAd != null)
                rewardedVideoAd.ShowAd();
        }

        public override bool IsRewardedVideoLoaded()
        {
            return rewardedVideoAd != null && rewardedVideoAd.IsAdReady();
        }

        // The Rewarded Video ad view has opened. Your activity will loose focus.
        private void RewardedVideoOnAdOpenedEvent(LevelPlayAdInfo adInfo)
        {
            AdsManager.CallEventInMainThread(delegate
            {
                AudioListener.pause = true;

                if (Monetization.VerboseLogging)
                    Debug.Log("[AdsManager]: RewardedVideoOnAdOpenedEvent event received");

                AdsManager.OnProviderAdDisplayed(AdProvider.LevelPlay, AdType.RewardedVideo);
            });
        }

        // The Rewarded Video ad view is about to be closed. Your activity will regain its focus.
        private void RewardedVideoOnAdClosedEvent(LevelPlayAdInfo info)
        {
            AdsManager.CallEventInMainThread(delegate
            {
                AudioListener.pause = false;

                if (Monetization.VerboseLogging)
                    Debug.Log("[AdsManager]: RewardedVideoOnAdClosedEvent event received");

                AdsManager.OnProviderAdClosed(AdProvider.LevelPlay, AdType.RewardedVideo);
            });
        }

        // The user completed to watch the video, and should be rewarded.
        // The placement parameter will include the reward data.
        // When using server-to-server callbacks, you may ignore this event and wait for the ironSource server callback.
        private void RewardedVideoOnAdRewardedEvent(LevelPlayAdInfo info, LevelPlayReward reward)
        {
            AdsManager.CallEventInMainThread(delegate
            {
                AdsManager.ExecuteRewardVideoCallback(true);

                if (Monetization.VerboseLogging)
                    Debug.Log("[AdsManager]: RewardedVideoOnAdRewardedEvent event received");

                AdsManager.ResetInterstitialDelayTime();
                AdsManager.RequestRewardBasedVideo();
            });
        }

        // The rewarded video ad was failed to show.
        private void RewardedVideoOnAdShowFailedEvent(LevelPlayAdInfo info, LevelPlayAdError error)
        {
            AdsManager.CallEventInMainThread(delegate
            {
                AdsManager.ExecuteRewardVideoCallback(false);

                if (Monetization.VerboseLogging)
                    Debug.Log("[AdsManager]: RewardedVideoOnAdShowFailedEvent event received with message: " + error);

                HandleAdLoadFailure(AdType.RewardedVideo, error.ErrorMessage, ref rewardedRetryAttempt, () => RequestRewardedVideo());
            });
        }

        /// <summary>
        /// Retrieves the Rewarded Video ID based on the platform
        /// </summary>
        public string GetRewardedVideoID()
        {
#if UNITY_EDITOR
            return "unused";
#elif UNITY_ANDROID
            return adsSettings.LevelPlayContainer.AndroidRVID;
#elif UNITY_IOS
            return adsSettings.LevelPlayContainer.IOSRVID;
#else
            return "unexpected_platform";
#endif
        }
        #endregion

        #region Interstitial
        public override void RequestInterstitial()
        {
            if (interstitialAd != null)
                interstitialAd.LoadAd();
        }

        public override void ShowInterstitial(AdvertisementCallback callback)
        {
            if(interstitialAd != null)
                interstitialAd.ShowAd();
        }

        public override bool IsInterstitialLoaded()
        {
            return interstitialAd != null && interstitialAd.IsAdReady();
        }

        private void InterstitialOnAdClosedEvent(LevelPlayAdInfo info)
        {
            AdsManager.CallEventInMainThread(delegate
            {
                AudioListener.pause = false;

                if (Monetization.VerboseLogging)
                    Debug.Log("[AdsManager]: InterstitialOnAdClosedEvent event received");

                AdsManager.OnProviderAdClosed(AdProvider.LevelPlay, AdType.Interstitial);

                AdsManager.ExecuteInterstitialCallback(true);

                AdsManager.ResetInterstitialDelayTime();
                AdsManager.RequestInterstitial();
            });
        }

        private void InterstitialOnAdDisplayFailedEvent(LevelPlayAdInfo info, LevelPlayAdError error)
        {
            AdsManager.CallEventInMainThread(delegate
            {
                if (Monetization.VerboseLogging)
                    Debug.Log("[AdsManager]: Interstitial ad failed to load an ad with error: " + error.ErrorMessage);

                HandleAdLoadFailure(AdType.Interstitial, error.ErrorMessage, ref interstitialRetryAttempt, () => RequestInterstitial());
            });
        }

        private void InterstitialOnAdDisplayedEvent(LevelPlayAdInfo info)
        {
            AdsManager.CallEventInMainThread(delegate
            {
                AudioListener.pause = true;

                if (Monetization.VerboseLogging)
                    Debug.Log("[AdsManager]: InterstitialOnAdDisplayedEvent event received");

                AdsManager.OnProviderAdDisplayed(AdProvider.LevelPlay, AdType.Interstitial);
            });
        }

        private void InterstitialOnAdLoadFailedEvent(LevelPlayAdError error)
        {
            AdsManager.CallEventInMainThread(delegate
            {
                if (Monetization.VerboseLogging)
                    Debug.Log("[AdsManager]: Interstitial ad failed to load an ad with error: " + error.ErrorMessage);

                HandleAdLoadFailure(AdType.Interstitial, error.ErrorMessage, ref interstitialRetryAttempt, () => RequestInterstitial());
            });
        }

        private void InterstitialOnAdLoadedEvent(LevelPlayAdInfo info)
        {
            AdsManager.CallEventInMainThread(delegate
            {
                if (Monetization.VerboseLogging)
                    Debug.Log("[AdsManager]: Interstitial ad loaded");

                interstitialRetryAttempt = RETRY_ATTEMPT_DEFAULT_VALUE;

                AdsManager.OnProviderAdLoaded(AdProvider.LevelPlay, AdType.Interstitial);
            });
        }

        /// <summary>
        /// Retrieves the interstitial ID based on the platform
        /// </summary>
        public string GetInterstitialID()
        {
#if UNITY_EDITOR
            return "unused";
#elif UNITY_ANDROID
            return adsSettings.LevelPlayContainer.AndroidInterstitialID;
#elif UNITY_IOS
            return adsSettings.LevelPlayContainer.IOSInterstitialID;
#else
            return "unexpected_platform";
#endif
        }
        #endregion

        #region Banner
        public override void DestroyBanner()
        {
            if(bannerAd != null)
            {
                bannerAd.DestroyAd();
                bannerAd = null;
            }

            AdsManager.ResetBannerLayout();
            AdsManager.OnProviderAdClosed(AdProvider.LevelPlay, AdType.Banner);
        }

        public override void HideBanner()
        {
            if (bannerAd != null)
            {
                bannerAd.HideAd();
            }

            AdsManager.ResetBannerLayout();
            AdsManager.OnProviderAdClosed(AdProvider.LevelPlay, AdType.Banner);
        }

        public override void ShowBanner()
        {
            if(bannerAd != null)
            {
                bannerAd.ShowAd();
                UpdateBannerLayout();
            }

            AdsManager.OnProviderAdDisplayed(AdProvider.LevelPlay, AdType.Banner);
        }

        private void BannerOnAdLoadedEvent(LevelPlayAdInfo info)
        {
            AdsManager.CallEventInMainThread(delegate
            {
                if (Monetization.VerboseLogging)
                    Debug.Log("[AdsManager]: BannerOnAdLoadedEvent event received");

                AdsManager.OnProviderAdLoaded(AdProvider.LevelPlay, AdType.Banner);
                UpdateBannerLayout(info != null ? info.AdSize : null);
            });
        }

        private void BannerOnAdDisplayedEvent(LevelPlayAdInfo info)
        {
            UpdateBannerLayout(info != null ? info.AdSize : null);
            AdsManager.OnProviderAdDisplayed(AdProvider.LevelPlay, AdType.Banner);
        }

        private void UpdateBannerLayout(LevelPlayAdSize reportedSize = null)
        {
            LevelPlayAdSize adSize = reportedSize;
            if (adSize == null && bannerAd != null)
            {
                adSize = bannerAd.GetAdSize();
            }

            if (adSize != null && adSize.Width > 0 && adSize.Height > 0)
            {
                AdsManager.SetBannerLayout(AdProvider.LevelPlay, new Vector2Int(adSize.Width, adSize.Height));
                return;
            }

            // ShowBanner can happen before the SDK reports the loaded size. Reserve
            // only the standard 320x50 slot until the callback above updates it.
            AdsManager.SetBannerLayout(AdProvider.LevelPlay, new Vector2Int(320, 50));
        }

        /// <summary>
        /// Retrieves the banner ID based on the platform
        /// </summary>
        public string GetBannerID()
        {
#if UNITY_EDITOR
            return "unused";
#elif UNITY_ANDROID
            return adsSettings.LevelPlayContainer.AndroidBannerID;
#elif UNITY_IOS
            return adsSettings.LevelPlayContainer.IOSBannerID;
#else
            return "unexpected_platform";
#endif
        }
        #endregion

        public void OpenTestSuite()
        {
            LevelPlay.LaunchTestSuite();
        }

        public string GetAppKey()
        {
#if UNITY_EDITOR
            return "unused";
#elif UNITY_ANDROID
            return adsSettings.LevelPlayContainer.AndroidAppKey;
#elif UNITY_IOS
            return adsSettings.LevelPlayContainer.IOSAppKey;
#else
            return "unexpected_platform";
#endif
        }
    }
#endif
}
