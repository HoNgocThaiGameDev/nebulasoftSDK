
using System.Threading.Tasks;
using UnityEngine;

namespace NebulaSoft
{
    public class AdDummyHandler : AdProviderHandler
    {
        private AdDummyController dummyController;

        private bool isInterstitialLoaded = false;
        private bool isRewardVideoLoaded = false;

        public AdDummyHandler(AdProvider providerType) : base(providerType) { }

        protected override async Task<bool> InitProviderAsync()
        {
            if (Monetization.VerboseLogging)
                Debug.Log("[AdsManager]: Dummy Ads is trying to initialize!", adsSettings);

            TaskCompletionSource<bool> tcs = new TaskCompletionSource<bool>();

            if (adsSettings.IsDummyEnabled())
            {
                dummyController = AdDummyController.CreateObject();
                dummyController.Init(adsSettings);
            }

            if (Monetization.VerboseLogging)
                Debug.Log("[AdsManager]: Dummy Ads initialization complete");

            // Set task result to true indicating success
            tcs.SetResult(true);

            // Await the result of the initialization process
            return await tcs.Task;
        }

        public override void ShowBanner()
        {
            dummyController.ShowBanner();

            AdsManager.OnProviderAdDisplayed(providerType, AdType.Banner);

            AdsManager.SetBannerHeight(dummyController.GetBannerScreenHeight());
        }

        public override void HideBanner()
        {
            dummyController.HideBanner();

            AdsManager.ResetBannerLayout();
            AdsManager.OnProviderAdClosed(providerType, AdType.Banner);
        }

        public override void DestroyBanner()
        {
            dummyController.HideBanner();

            AdsManager.ResetBannerLayout();
            AdsManager.OnProviderAdClosed(providerType, AdType.Banner);
        }

        public override void RequestInterstitial()
        {
            isInterstitialLoaded = true;

            AdsManager.OnProviderAdLoaded(providerType, AdType.Interstitial);
        }

        public override bool IsInterstitialLoaded()
        {
            return isInterstitialLoaded;
        }

        public override void ShowInterstitial(AdvertisementCallback callback)
        {
            dummyController.ShowInterstitial();

            AdsManager.OnProviderAdDisplayed(providerType, AdType.Interstitial);
        }

        public override void RequestRewardedVideo()
        {
            isRewardVideoLoaded = true;

            AdsManager.OnProviderAdLoaded(providerType, AdType.RewardedVideo);
        }

        public override bool IsRewardedVideoLoaded()
        {
            return isRewardVideoLoaded;
        }

        public override void ShowRewardedVideo(AdvertisementCallback callback)
        {
            dummyController.ShowRewardedVideo();

            AdsManager.OnProviderAdDisplayed(providerType, AdType.RewardedVideo);
        }
    }
}
