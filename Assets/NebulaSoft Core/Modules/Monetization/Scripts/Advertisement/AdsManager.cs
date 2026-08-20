#pragma warning disable 0649
#pragma warning disable 0162

using UnityEngine;
using System.Collections.Generic;
using System.Collections;
using System.Collections.Concurrent;
using UnityEngine.SceneManagement;

namespace NebulaSoft
{
    /// <summary>
    /// The portion of the display occupied by the currently visible banner.
    /// All values are expressed in the provider's display pixels, with <see cref="TopEdge"/>
    /// measured from the bottom of the display. This keeps UI layout independent from the
    /// banner provider while still preserving the provider's real rendered size.
    /// </summary>
    public struct BannerLayoutInfo
    {
        public static readonly BannerLayoutInfo Hidden = new BannerLayoutInfo(
            AdProvider.Disable,
            false,
            Vector2Int.zero,
            0f);

        public AdProvider Provider { get; }
        public bool IsVisible { get; }
        public Vector2Int PixelSize { get; }
        public float TopEdge { get; }
        public float Height => IsVisible ? PixelSize.y : 0f;

        public BannerLayoutInfo(AdProvider provider, bool isVisible, Vector2Int pixelSize, float topEdge)
        {
            Provider = provider;
            IsVisible = isVisible && pixelSize.x > 0 && pixelSize.y > 0;
            PixelSize = IsVisible
                ? new Vector2Int(Mathf.Max(1, pixelSize.x), Mathf.Max(1, pixelSize.y))
                : Vector2Int.zero;
            TopEdge = IsVisible ? Mathf.Max(PixelSize.y, topEdge) : 0f;
        }
    }

    [StaticUnload]
    [Define("MODULE_ADMOB", "GoogleMobileAds.Api.MobileAds", "GoogleMobileAds.Unity.dll")]
    [Define("MODULE_ADSKIT", "VoxelBusters.AdsKit.AdsManager", "Assets/Plugins/VoxelBusters/AdsKit")]
    [Define("MODULE_UNITYADS", "UnityEngine.Advertisements.Advertisement", "Packages/com.unity.ads")]
    [Define("MODULE_LEVELPLAY", "Unity.Services.LevelPlay.LevelPlay", "Packages/com.unity.services.levelplay")]
    public static class AdsManager
    {
        private const int DEFAULT_BANNER_HEIGHT = 0;
        private const int INIT_ATTEMPTS_AMOUNT = 30;

        private const double FORCED_AD_DISABLED_FOREVER = -1;
        private const string FIRST_LAUNCH_PREFS = "FIRST_LAUNCH";

        private static AdProviderHandler[] AD_PROVIDERS;

        private static bool isModuleInitialized;

        private static AdsSettings settings;
        public static AdsSettings Settings => settings;

        private static double lastInterstitialTime;

        private static AdProviderHandler.AdvertisementCallback rewardedVideoCallback;
        private static AdProviderHandler.AdvertisementCallback interstitalCallback;

        private static readonly ConcurrentQueue<SimpleCallback> mainThreadEvents = new ConcurrentQueue<SimpleCallback>();

        private static bool isFirstAdLoaded = false;
        private static bool waitingForRewardVideoCallback;

        private static bool isBannerActive = true;
        public static bool IsBannerActive => isBannerActive;

        private static float bannerHeight = DEFAULT_BANNER_HEIGHT;
        public static float BannerHeight => bannerHeight;

        private static BannerLayoutInfo bannerLayout = BannerLayoutInfo.Hidden;
        public static BannerLayoutInfo BannerLayout => bannerLayout;

        private static Coroutine loadingCoroutine;
        private static TweenCase delayTweenCase;

        private static Dictionary<AdProvider, AdProviderHandler> advertisingActiveModules = new Dictionary<AdProvider, AdProviderHandler>();

        private static float intFirstStartDelay;
        private static float intStartDelay;
        private static float intShowingDelay;

        // Events
        public static event SimpleCallback ForcedAdDisabled;
        /// <summary>Raised when the permanent No Ads entitlement changes.</summary>
        public static event System.Action<bool> NoAdsEntitlementChanged;
        /// <summary>Raised once when a rewarded video grants its reward to the player.</summary>
        public static event SimpleCallback RewardedVideoRewarded;
        /// <summary>Raised whenever a banner's visible size or placement changes.</summary>
        public static event System.Action<BannerLayoutInfo> BannerLayoutChanged;

        public static event AdsModuleCallback AdProviderInitialized;
        public static event AdsEventsCallback AdLoaded;
        public static event AdsEventsCallback AdDisplayed;
        public static event AdsEventsCallback AdClosed;

        public static AdsBoolCallback InterstitialConditions;

        private static AdSave save;

        #region Initialize
        public static void Init(MonetizationSettings monetizationSettings)
        {
            if (isModuleInitialized)
            {
                Debug.LogWarning("[AdsManager]: Module already exists!");

                return;
            }

            isModuleInitialized = true;
            isFirstAdLoaded = false;

            settings = monetizationSettings.AdsSettings;

            intFirstStartDelay = settings.InterstitialFirstStartDelay;
            intStartDelay = settings.InterstitialStartDelay;
            intShowingDelay = settings.InterstitialShowingDelay;

            AdsRemoteConfigData remoteConfigData = RemoteConfigController.TryGetConfig<AdsRemoteConfigData>("ads");
            if (remoteConfigData != null)
            {
                intFirstStartDelay = remoteConfigData.intFSDelay;
                intStartDelay = remoteConfigData.intSDelay;
                intShowingDelay = remoteConfigData.intDelay;

                if(!remoteConfigData.useBanner)
                    settings.DisableBanner();

                if (!remoteConfigData.useInterstitials)
                    settings.DisableInterstitial();
            }

            save = SaveController.GetSaveObject<AdSave>("advertisement_forced_ad");

            if (settings == null)
            {
                Debug.LogError("[AdsManager]: Settings don't exist!");

                return;
            }

            AD_PROVIDERS = GetProviders();

            if (!PlayerPrefs.HasKey(FIRST_LAUNCH_PREFS))
            {
                lastInterstitialTime = Time.time + intFirstStartDelay;

                PlayerPrefs.SetInt(FIRST_LAUNCH_PREFS, 1);
            }
            else
            {
                lastInterstitialTime = Time.time + intStartDelay;
            }

            // Check if ad-free period has expired and send analytics event only once
            if (save.ForcedAdDisabledUntil != 0 && save.ForcedAdDisabledUntil != FORCED_AD_DISABLED_FOREVER && IsForcedAdEnabled())
            {
                AnalyticsController.TrackEvent(AnalyticsEventType.AdFreePeriodExpired);

                save.ForcedAdDisabledUntil = 0;
            }

            Initializer.GameObject.AddComponent<AdsManager.AdEventExecutor>();

            advertisingActiveModules = new Dictionary<AdProvider, AdProviderHandler>();
            for (int i = 0; i < AD_PROVIDERS.Length; i++)
            {
                if (IsModuleEnabled(AD_PROVIDERS[i].ProviderType))
                {
                    AD_PROVIDERS[i].LinkSettings(Monetization.Settings);

                    advertisingActiveModules.Add(AD_PROVIDERS[i].ProviderType, AD_PROVIDERS[i]);
                }
            }

            if (Monetization.VerboseLogging)
            {
                if (settings.BannerType != AdProvider.Disable && !advertisingActiveModules.ContainsKey(settings.BannerType))
                    Debug.LogWarning("[AdsManager]: Banner type (" + settings.BannerType + ") is selected, but isn't active!");

                if (settings.InterstitialType != AdProvider.Disable && !advertisingActiveModules.ContainsKey(settings.InterstitialType))
                    Debug.LogWarning("[AdsManager]: Interstitial type (" + settings.InterstitialType + ") is selected, but isn't active!");

                if (settings.RewardedVideoType != AdProvider.Disable && !advertisingActiveModules.ContainsKey(settings.RewardedVideoType))
                    Debug.LogWarning("[AdsManager]: Rewarded Video type (" + settings.RewardedVideoType + ") is selected, but isn't active!");
            }

            SceneManager.activeSceneChanged -= OnActiveSceneChanged;
            SceneManager.activeSceneChanged += OnActiveSceneChanged;

            FirebaseNoAdsEntitlementSync.ApplyPendingCloudState();
            InitializeModules(settings.LoadAdsOnStart);
        }

        private static async void InitializeModules(bool loadAds)
        {
            // Loop through all the providers and initialize them asynchronously
            foreach (AdProviderHandler providerHandler in advertisingActiveModules.Values)
            {
                Debug.Log($"[AdsManager]: {providerHandler.ProviderType} is trying to initialize!");

                bool isInitialized = await providerHandler.InitAsync();

                if (isInitialized)
                {
                    if (Monetization.VerboseLogging)
                        Debug.Log($"[AdsManager]: {providerHandler.ProviderType} initialized successfully.");
                }
                else
                {
                    Debug.LogError($"[AdsManager]: {providerHandler.ProviderType} failed to initialize.");
                }
            }

            if (loadAds)
            {
                TryToLoadFirstAds();
            }
        }
        #endregion

        private static void Update()
        {
            if (!isModuleInitialized)
                return;

            while (mainThreadEvents.TryDequeue(out var a))
            {
                try
                {
                    a?.Invoke();
                }
                catch (System.Exception ex)
                {
                    Debug.LogException(ex);
                }
            }
        }

        public static void TryToLoadFirstAds()
        {
            if (loadingCoroutine == null)
            {
                Debug.Log("[AdsManager]: Loading first ads..");

                loadingCoroutine = Tween.InvokeCoroutine(TryToLoadAdsCoroutine());
            }
        }

        private static IEnumerator TryToLoadAdsCoroutine()
        {
            int initAttemps = 0;

            yield return new WaitForSeconds(1.0f);

            while (!isFirstAdLoaded && initAttemps < INIT_ATTEMPTS_AMOUNT)
            {
                if (LoadFirstAds())
                    break;

                yield return new WaitForSeconds(1.0f * (initAttemps + 1));

                initAttemps++;
            }

            if (Monetization.VerboseLogging)
                Debug.Log("[AdsManager]: First ads have loaded!");
        }

        private static bool LoadFirstAds()
        {
            if (!isModuleInitialized)
                return false;

            if (isFirstAdLoaded)
                return true;
            
            bool isRewardedVideoModuleInititalized = AdsManager.IsModuleInititalized(AdsManager.Settings.RewardedVideoType);
            bool isInterstitialModuleInitialized = AdsManager.IsModuleInititalized(AdsManager.Settings.InterstitialType);
            bool isBannerModuleInitialized = AdsManager.IsModuleInititalized(AdsManager.Settings.BannerType);

            bool isRewardedVideoActive = AdsManager.Settings.RewardedVideoType != AdProvider.Disable;
            bool isInterstitialActive = AdsManager.Settings.InterstitialType != AdProvider.Disable;
            bool isBannerActive = AdsManager.Settings.BannerType != AdProvider.Disable;

            if ((!isRewardedVideoActive || isRewardedVideoModuleInititalized) && (!isInterstitialActive || isInterstitialModuleInitialized) && (!isBannerActive || isBannerModuleInitialized))
            {
                if (isRewardedVideoActive)
                    AdsManager.RequestRewardBasedVideo();

                bool isForcedAdEnabled = AdsManager.IsForcedAdEnabled();
                if (isInterstitialActive && isForcedAdEnabled)
                    AdsManager.RequestInterstitial();

                if (isBannerActive && isForcedAdEnabled)
                    AdsManager.ShowBanner();

                isFirstAdLoaded = true;

                return true;
            }

            return false;
        }

        public static void CallEventInMainThread(SimpleCallback callback)
        {
            if (callback != null)
            {
                mainThreadEvents.Enqueue(callback);
            }
        }

        public static void ShowErrorMessage()
        {
            SystemMessage.ShowMessage("Network error. Please try again later");
        }

        public static bool IsModuleEnabled(AdProvider advertisingModule)
        {
            if (!Monetization.IsActive || !isModuleInitialized)
                return false;

            if (advertisingModule == AdProvider.Disable)
                return false;

            return (Settings.BannerType == advertisingModule || Settings.InterstitialType == advertisingModule || Settings.RewardedVideoType == advertisingModule);
        }

        public static AdProviderHandler GetAdProvider(AdProvider adProvider)
        {
            if(advertisingActiveModules.ContainsKey(adProvider))
            {
                return advertisingActiveModules[adProvider];
            }

            return null;
        }

        public static bool IsModuleActive(AdProvider advertisingModule)
        {
            return advertisingActiveModules.ContainsKey(advertisingModule);
        }

        public static bool IsModuleInititalized(AdProvider advertisingModule)
        {
            if (advertisingActiveModules.ContainsKey(advertisingModule))
            {
                return advertisingActiveModules[advertisingModule].IsInitialized;
            }

            return false;
        }

        #region Interstitial
        public static bool IsInterstitialLoaded()
        {
            if (!Monetization.IsActive || !isModuleInitialized)
            {
                Debug.LogWarning("[IAP Manager]: Mobile monetization is disabled!");

                return false;
            }

            AdProvider advertisingModules = settings.InterstitialType;

            if (!IsForcedAdEnabled() || !IsModuleActive(advertisingModules))
                return false;

            return advertisingActiveModules[advertisingModules].IsInterstitialLoaded();
        }

        public static void RequestInterstitial()
        {
            if (!Monetization.IsActive || !isModuleInitialized)
            {
                Debug.LogWarning("[IAP Manager]: Mobile monetization is disabled!");

                return;
            }

            AdProvider advertisingModules = settings.InterstitialType;

            if (!IsForcedAdEnabled() || !IsModuleActive(advertisingModules) || !advertisingActiveModules[advertisingModules].IsInitialized || advertisingActiveModules[advertisingModules].IsInterstitialLoaded())
                return;

            advertisingActiveModules[advertisingModules].RequestInterstitial();
        }

        public static void ShowInterstitial(AdProviderHandler.AdvertisementCallback callback, string analyticsEvent = "Default", bool ignoreConditions = false)
        {
            AdProvider advertisingModules = settings.InterstitialType;

            interstitalCallback = callback;
            interstitalCallback += (result) =>
            {
                if (string.IsNullOrEmpty(analyticsEvent))
                    analyticsEvent = "Default";

                AnalyticsController.OnInterstitialDisplayed(analyticsEvent);
            };

            if (!Monetization.IsActive || !isModuleInitialized)
            {
                Debug.LogWarning("[IAP Manager]: Mobile monetization is disabled!");

                ExecuteInterstitialCallback(false);

                return;
            }

            if (!IsForcedAdEnabled() || !IsModuleActive(advertisingModules) || (!ignoreConditions && (!CheckInterstitialTime() || !CheckExtraInterstitialCondition())) || !advertisingActiveModules[advertisingModules].IsInitialized || !advertisingActiveModules[advertisingModules].IsInterstitialLoaded())
            {
                ExecuteInterstitialCallback(false);

                return;
            }

            delayTweenCase.KillActive();

            if (settings.LoadingAdDuration > 0)
            {
                SystemMessage.ShowLoadingPanel();
                SystemMessage.ChangeLoadingMessage(settings.LoadingMessage);

                delayTweenCase.KillActive();
                delayTweenCase = Tween.DelayedCall(settings.LoadingAdDuration, () =>
                {
                    advertisingActiveModules[advertisingModules].ShowInterstitial(callback);

                    SystemMessage.HideLoadingPanel();
                }, unscaledTime: true);
            }
            else
            {
                advertisingActiveModules[advertisingModules].ShowInterstitial(callback);
            }
        }

        public static void ExecuteInterstitialCallback(bool result)
        {
            if (interstitalCallback != null)
            {
                CallEventInMainThread(() => interstitalCallback.Invoke(result));
            }
        }

        public static void SetInterstitialDelayTime(float time)
        {
            lastInterstitialTime = Time.time + time;
        }

        public static void ResetInterstitialDelayTime()
        {
            lastInterstitialTime = Time.time + intShowingDelay;
        }

        private static bool CheckInterstitialTime()
        {
            if (Monetization.VerboseLogging)
                Debug.Log("[AdsManager]: Interstitial Time: " + lastInterstitialTime + "; Time: " + Time.time);

            return lastInterstitialTime < Time.time;
        }

        public static bool CheckExtraInterstitialCondition()
        {
            if (InterstitialConditions != null)
            {
                bool state = true;

                System.Delegate[] listDelegates = InterstitialConditions.GetInvocationList();
                for (int i = 0; i < listDelegates.Length; i++)
                {
                    if (!(bool)listDelegates[i].DynamicInvoke())
                    {
                        state = false;

                        break;
                    }
                }

                if (Monetization.VerboseLogging)
                    Debug.Log("[AdsManager]: Extra condition interstitial state: " + state);

                return state;
            }

            return true;
        }
        #endregion

        #region Rewarded Video
        public static bool IsRewardBasedVideoLoaded()
        {
            if (!Monetization.IsActive || !isModuleInitialized)
            {
                Debug.LogWarning("[IAP Manager]: Mobile monetization is disabled!");

                return false;
            }

            AdProvider advertisingModule = settings.RewardedVideoType;

            if (!IsModuleActive(advertisingModule) || !advertisingActiveModules[advertisingModule].IsInitialized)
                return false;

            return advertisingActiveModules[advertisingModule].IsRewardedVideoLoaded();
        }

        public static void RequestRewardBasedVideo()
        {
            if (!Monetization.IsActive || !isModuleInitialized)
            {
                Debug.LogWarning("[IAP Manager]: Mobile monetization is disabled!");

                return;
            }

            AdProvider advertisingModule = settings.RewardedVideoType;

            if (!IsModuleActive(advertisingModule) || !advertisingActiveModules[advertisingModule].IsInitialized || advertisingActiveModules[advertisingModule].IsRewardedVideoLoaded())
                return;

            advertisingActiveModules[advertisingModule].RequestRewardedVideo();
        }

        public static void ShowRewardBasedVideo(AdProviderHandler.AdvertisementCallback callback, string analyticsEvent = "Default", bool showErrorMessage = true)
        {
            rewardedVideoCallback = callback;
            waitingForRewardVideoCallback = true;

            if (!Monetization.IsActive || !isModuleInitialized)
            {
                Debug.LogWarning("[IAP Manager]: Mobile monetization is disabled!");

                ExecuteRewardVideoCallback(false);

                return;
            }

            AdProvider advertisingModule = settings.RewardedVideoType;
            
            if (!IsModuleActive(advertisingModule) || !advertisingActiveModules[advertisingModule].IsInitialized || !advertisingActiveModules[advertisingModule].IsRewardedVideoLoaded())
            {
                ExecuteRewardVideoCallback(false);

                if (showErrorMessage)
                    ShowErrorMessage();

                return;
            }

            if (string.IsNullOrEmpty(analyticsEvent))
                analyticsEvent = "Default";

            AnalyticsController.OnRVClicked(analyticsEvent);

            delayTweenCase.KillActive();

            if (settings.LoadingAdDuration > 0)
            {
                SystemMessage.ShowLoadingPanel();
                SystemMessage.ChangeLoadingMessage(settings.LoadingMessage);

                delayTweenCase.KillActive();
                delayTweenCase = Tween.DelayedCall(settings.LoadingAdDuration, () =>
                {
                    advertisingActiveModules[advertisingModule].ShowRewardedVideo(callback);

                    SystemMessage.HideLoadingPanel();
                }, unscaledTime: true);
            }
            else
            {
                advertisingActiveModules[advertisingModule].ShowRewardedVideo(callback);
            }
        }

        public static void ExecuteRewardVideoCallback(bool result)
        {
            if (rewardedVideoCallback != null && waitingForRewardVideoCallback)
            {
                AdProviderHandler.AdvertisementCallback callback = rewardedVideoCallback;
                CallEventInMainThread(() =>
                {
                    if (result)
                        RewardedVideoRewarded?.Invoke();

                    callback.Invoke(result);
                });

                waitingForRewardVideoCallback = false;

                if (Monetization.VerboseLogging)
                {
                    Debug.Log("[AdsManager]: Reward received: " + result);
                }
            }
        }
        #endregion

        #region Banner
        public static void ShowBanner()
        {
            if (!Monetization.IsActive || !isModuleInitialized)
            {
                Debug.LogWarning("[AdsManager]: Mobile monetization is disabled!");

                return;
            }

            if (!IsBannerDisplayAllowed())
            {
                HideBanner();
                return;
            }

            if (!isBannerActive) return;

            AdProvider advertisingModule = settings.BannerType;

            if (!IsForcedAdEnabled() || !IsModuleActive(advertisingModule) || !advertisingActiveModules[advertisingModule].IsInitialized)
                return;

            advertisingActiveModules[advertisingModule].ShowBanner();
        }

        private static void OnActiveSceneChanged(Scene previousScene, Scene nextScene)
        {
            if (!isModuleInitialized)
                return;

            if (IsBannerDisplayAllowed())
            {
                if (isBannerActive)
                    ShowBanner();

                return;
            }

            HideBanner();
        }

        private static bool IsBannerDisplayAllowed()
        {
            return Object.FindFirstObjectByType<LoadingGraphics>(FindObjectsInactive.Exclude) == null;
        }

        public static void DestroyBanner()
        {
            ResetBannerLayout();

            if (!Monetization.IsActive || !isModuleInitialized)
            {
                Debug.LogWarning("[AdsManager]: Mobile monetization is disabled!");

                return;
            }

            AdProvider advertisingModule = settings.BannerType;

            if (!IsModuleActive(advertisingModule) || !advertisingActiveModules[advertisingModule].IsInitialized)
                return;

            advertisingActiveModules[advertisingModule].DestroyBanner();
        }

        public static void HideBanner()
        {
            ResetBannerLayout();

            if (!Monetization.IsActive || !isModuleInitialized)
            {
                Debug.LogWarning("[AdsManager]: Mobile monetization is disabled!");

                return;
            }

            AdProvider advertisingModule = settings.BannerType;

            if (!IsModuleActive(advertisingModule) || !advertisingActiveModules[advertisingModule].IsInitialized)
                return;

            advertisingActiveModules[advertisingModule].HideBanner();
        }

        public static void EnableBanner()
        {
            if (!Monetization.IsActive || !isModuleInitialized)
            {
                Debug.LogWarning("[AdsManager]: Mobile monetization is disabled!");

                return;
            }

            isBannerActive = true;

            NotchSaveArea.Refresh(true);

            ShowBanner();
        }

        public static void DisableBanner()
        {
            if (!Monetization.IsActive || !isModuleInitialized)
            {
                Debug.LogWarning("[AdsManager]: Mobile monetization is disabled!");

                return;
            }

            isBannerActive = false;

            HideBanner();
        }

        public static void ResetBannerHeight()
        {
            ResetBannerLayout();
        }

        public static void SetBannerHeight(float height)
        {
            if (height <= 0f)
            {
                ResetBannerLayout();
                return;
            }

            AdProvider provider = Settings != null ? Settings.BannerType : AdProvider.Disable;
            SetBannerLayout(
                provider,
                new Vector2Int(Mathf.Max(1, Screen.width), Mathf.CeilToInt(height)));
        }

        /// <summary>
        /// Sets the canonical banner layout reported by the active ad provider.
        /// <paramref name="bottomInset"/> is the banner's actual distance from the
        /// display bottom, not an artificial UI gap.
        /// </summary>
        public static void SetBannerLayout(AdProvider provider, Vector2Int pixelSize, float bottomInset = 0f)
        {
            if (pixelSize.x <= 0 || pixelSize.y <= 0)
            {
                ResetBannerLayout();
                return;
            }

            float topEdge = Mathf.Max(0f, bottomInset) + pixelSize.y;
            ApplyBannerLayout(new BannerLayoutInfo(provider, true, pixelSize, topEdge));
        }

        public static void ResetBannerLayout()
        {
            ApplyBannerLayout(BannerLayoutInfo.Hidden);
        }

        private static void ApplyBannerLayout(BannerLayoutInfo layout)
        {
            bool changed = bannerLayout.Provider != layout.Provider ||
                           bannerLayout.IsVisible != layout.IsVisible ||
                           bannerLayout.PixelSize != layout.PixelSize ||
                           !Mathf.Approximately(bannerLayout.TopEdge, layout.TopEdge);

            bannerLayout = layout;
            bannerHeight = layout.Height;

            if (!changed)
                return;

            NotchSaveArea.Refresh(true);
            BannerLayoutChanged?.Invoke(layout);
        }

        public static float GetBannerHeight()
        {
            if (Settings != null && Settings.BannerType != AdProvider.Disable && IsForcedAdEnabled() && IsBannerActive && bannerLayout.IsVisible)
            {
                return bannerLayout.Height;
            }

            return 0.0f;
        }

        public static float GetBannerTopEdge()
        {
            if (Settings != null && Settings.BannerType != AdProvider.Disable && IsForcedAdEnabled() && IsBannerActive && bannerLayout.IsVisible)
            {
                return bannerLayout.TopEdge;
            }

            return 0.0f;
        }
        #endregion

        #region Forced Ad
        public static bool IsForcedAdEnabled()
        {
            if (save == null) return true;

            if (save.ForcedAdDisabledUntil == FORCED_AD_DISABLED_FOREVER)
                return false;

            return TimeUtils.GetCurrentUnixTimestamp() >= save.ForcedAdDisabledUntil;
        }

        public static void DisableForcedAdForever()
        {
            DisableForcedAdInternal(FORCED_AD_DISABLED_FOREVER);
        }

        /// <summary>
        /// Applies a restored No Ads entitlement. A false value only clears the
        /// permanent No Ads flag; timed ad-free rewards are left intact.
        /// </summary>
        public static void SetNoAdsEntitlement(bool noAds)
        {
            if (save == null)
                return;

            if (noAds)
            {
                if (save.ForcedAdDisabledUntil != FORCED_AD_DISABLED_FOREVER)
                    DisableForcedAdForever();

                return;
            }

            if (save.ForcedAdDisabledUntil != FORCED_AD_DISABLED_FOREVER)
                return;

            save.ForcedAdDisabledUntil = 0;
            SaveController.MarkAsSaveIsRequired();

            if (isModuleInitialized)
                EnableBanner();

            NoAdsEntitlementChanged?.Invoke(false);
        }

        public static void DisableForcedAd(int durationSeconds)
        {
            if (durationSeconds < 0)
            {
                Debug.LogWarning("[AdsManager]: Invalid duration for ad disable: " + durationSeconds);

                return;
            }

            double targetTime = TimeUtils.GetCurrentUnixTimestamp() + durationSeconds;
            if (targetTime <= save.ForcedAdDisabledUntil)
                return;

            DisableForcedAdInternal(targetTime);
        }

        private static void DisableForcedAdInternal(double targetTime)
        {
            bool hadPermanentNoAds = save.ForcedAdDisabledUntil == FORCED_AD_DISABLED_FOREVER;

            Debug.Log("[AdsManager]: Banners and interstitials are disabled!");

            save.ForcedAdDisabledUntil = targetTime;

            SaveController.MarkAsSaveIsRequired();

            ResetBannerLayout();

            ForcedAdDisabled?.Invoke();

            DestroyBanner();

            bool hasPermanentNoAds = save.ForcedAdDisabledUntil == FORCED_AD_DISABLED_FOREVER;
            if (hadPermanentNoAds != hasPermanentNoAds)
                NoAdsEntitlementChanged?.Invoke(hasPermanentNoAds);
        }
        #endregion

        public static void OnProviderInitialized(AdProvider advertisingModule)
        {
            AdProviderInitialized?.Invoke(advertisingModule);
        }

        public static void OnProviderAdLoaded(AdProvider advertisingModule, AdType advertisingType)
        {
            AdLoaded?.Invoke(advertisingModule, advertisingType);
        }

        public static void OnProviderAdDisplayed(AdProvider advertisingModule, AdType advertisingType)
        {
            AdDisplayed?.Invoke(advertisingModule, advertisingType);

            if (advertisingType == AdType.Interstitial || advertisingType == AdType.RewardedVideo)
            {
                ResetInterstitialDelayTime();
            }
        }

        public static void OnProviderAdClosed(AdProvider advertisingModule, AdType advertisingType)
        {
            AdClosed?.Invoke(advertisingModule, advertisingType);

            if (advertisingType == AdType.Interstitial || advertisingType == AdType.RewardedVideo)
            {
                ResetInterstitialDelayTime();
            }
        }

        private static AdProviderHandler[] GetProviders()
        {
            return new AdProviderHandler[]
            {
                new AdDummyHandler(AdProvider.Dummy),

#if MODULE_ADSKIT
                new AdsKitAdMobHandler(AdProvider.AdMob),
#elif MODULE_ADMOB
                new AdMobHandler(AdProvider.AdMob),
#endif

#if MODULE_UNITYADS
                new UnityAdsLegacyHandler(AdProvider.UnityAdsLegacy), 
#endif

#if MODULE_LEVELPLAY
                new LevelPlayHandler(AdProvider.LevelPlay),
#endif

#if MODULE_APPLOVIN
                new ApplovinHandler(AdProvider.Applovin),
#endif
            };
        }

        private static void UnloadStatic()
        {
            SceneManager.activeSceneChanged -= OnActiveSceneChanged;

            isModuleInitialized = false;

            settings = null;
            lastInterstitialTime = 0;

            rewardedVideoCallback = null;
            interstitalCallback = null;

            mainThreadEvents.Clear();

            isFirstAdLoaded = false;
            waitingForRewardVideoCallback = false;

            isBannerActive = true;
            bannerHeight = DEFAULT_BANNER_HEIGHT;
            bannerLayout = BannerLayoutInfo.Hidden;

            loadingCoroutine = null;

            advertisingActiveModules.Clear();

            ForcedAdDisabled = null;
            NoAdsEntitlementChanged = null;
            RewardedVideoRewarded = null;
            BannerLayoutChanged = null;

            AdProviderInitialized = null;
            AdLoaded = null;
            AdDisplayed = null;
            AdClosed = null;

            InterstitialConditions = null;

            save = null;

            AD_PROVIDERS = null;
        }

        public delegate void AdsModuleCallback(AdProvider advertisingModules);
        public delegate void AdsEventsCallback(AdProvider advertisingModules, AdType advertisingType);
        public delegate bool AdsBoolCallback();

        private class AdEventExecutor : MonoBehaviour
        {
            private void Update()
            {
                AdsManager.Update();
            }
        }
    }
}

// -----------------
// Advertisement v1.4.2
// -----------------

// Changelog
// v1.4.2
// • Added EnableBanner, DisableBanner methods
// v1.4.1
// • Added ironSource (Unity LevelPlay) ad provider
// v1.4
// • Admob v9.0.0 support
// • Better naming and code cleanup
// • Ads callbacks replaced with simplified ones (AdLoaded, AdDisplayed, AdClosed)
// • Removed ShowInterstitial, ShowRewardedVideo, ShowBanner methods with provider type parameter
// • Added optional bool parameter to ShowInterstitial method. Allows to show interstitial even if conditions aren't met
// v1.3
// • Admob v8.1.0 support
// • Removed IronSource provider
// v1.2.1
// • Some fixes in IronSourse provider
// • Some fixes in Admob provider
// • New interface in Admob provider
// • Added Build Preprocessing for Admob 
// v1.2
// • Added IronSource provider
// v1.1f3
// • GDPR style rework
// • Rewarded video error message
// • Removed GDPR check in AdMob module
// v1.1f2
// • GDPR init bug fixed
// v1.1
// • Added first ad loader
// • Moved IAP check to AdsManager script
// v1.0
// • Added documentation
// v0.3
// • Unity Ads fixed
// v0.2
// • Bug fix
// v0.1
// • Added basic version
