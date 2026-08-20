using UnityEngine;

namespace NebulaSoft
{
    [System.Serializable]
    public class IAPItem
    {
        [SerializeField] string androidID;
        [SerializeField] string iOSID;

        [SerializeField] ProductKeyType productKeyType;
        [SerializeField] ProductType productType;

        [SerializeField] float defaultUSDPrice = 0.99f;

        [CreateScriptableObject]
        [SerializeField] RewardsSet rewardsSet;

        public string ID
        {
            get
            {
#if UNITY_ANDROID
                return androidID;
#elif UNITY_IOS
                return iOSID;
#else
                return string.Format("unknown_platform_{0}", productKeyType);
#endif
            }
        }

        public ProductType ProductType { get => productType; set => productType = value; }
        public ProductKeyType ProductKeyType { get => productKeyType; set => productKeyType = value; }
        public float DefaultUSDPrice { get => defaultUSDPrice; }

        public int TimesPurchased => save.TimesPurchased;
        public RewardsSet RewardsSet => rewardsSet;

        private Save save;

        public void Init()
        {
            save = SaveController.GetSaveObject<Save>($"iap_{productKeyType}");
        }

        public void OnProductPurchased()
        {
            save.TimesPurchased++;

            rewardsSet?.ApplyReward();
        }

        public void OnProductRestored()
        {
            rewardsSet?.RestoreReward();
        }

        public void OverrideDefaultPrice(IAPRemoteConfigData.IAP remoteConfigItem)
        {
            if (remoteConfigItem != null)
                defaultUSDPrice = remoteConfigItem.price;
        }

        public class Save : ISaveObject
        {
            public int TimesPurchased;

            public bool IsPurchased => TimesPurchased > 0;

            public void Flush()
            {

            }
        }
    }
}
