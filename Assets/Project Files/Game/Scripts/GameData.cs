using UnityEngine;

namespace NebulaSoft
{
    [CreateAssetMenu(fileName = "Game Data", menuName = "Data/Game Data")]
    public class GameData : ScriptableObject
    {
        [Space]
        [SerializeField] CurrencyType revieCurrencyType = CurrencyType.Coins;
        public CurrencyType ReviveCurrencyType => revieCurrencyType;

        [SerializeField] int[] revivePrices = { 900, 1200, 1500, 2000, 2500 };
        public int[] RevivePrices => revivePrices;

        [SerializeField] int reviveExtraSeconds = 20;
        public int ReviveExtraSeconds => reviveExtraSeconds;

        [SerializeField] int reviveIAPSeconds = 20;
        public int ReviveIAPSeconds => reviveIAPSeconds;

        [Space]
        [SerializeField] int showAdsAfterLevel = 5;
        public int ShowAdsAfterLevel => showAdsAfterLevel;

        [Space]
        [SerializeField] int skipReturingToMenuUntilLevel = 15;
        public int SkipReturningToMenuUntilLevel => skipReturingToMenuUntilLevel;

        [Header("Level Reward")]
        [SerializeField] CurrencyAmount defaultReward = new CurrencyAmount(CurrencyType.Coins, 20);
        public CurrencyAmount DefaultReward => defaultReward;

        [SerializeField] CurrencyAmount bossReward = new CurrencyAmount(CurrencyType.Coins, 40);
        public CurrencyAmount BossReward => bossReward;

        [SerializeField] int rewardMultiplier = 2;
        public int RewardMultiplier => rewardMultiplier;

        [SerializeField] int winStreakBonusPerWin = 5;
        [SerializeField] int winStreakBonusCap = 50;

        [Header("Block Highlight")]
        [ColorUsage(true, true)]
        [SerializeField] Color allowedColor;
        public Color AllowedColor => allowedColor;

        [ColorUsage(true, true)]
        [SerializeField] Color blockedColor;
        public Color BlockedColor => blockedColor;


        public static GameData Data { get; private set; }

        public void Init()
        {
            Data = this;

            AdsRemoteConfigData adsOverrideData = RemoteConfigController.TryGetConfig<AdsRemoteConfigData>("ads");
            if(adsOverrideData != null)
            {
                showAdsAfterLevel = adsOverrideData.minLevel;
            }

            ReviveRemoteConfigData reviveOverrideData = RemoteConfigController.TryGetConfig<ReviveRemoteConfigData>("revive");
            if(reviveOverrideData != null)
            {
                CurrencyType currencyType;
                if (System.Enum.TryParse(reviveOverrideData.currency, false, out currencyType))
                    revieCurrencyType = currencyType;

                revivePrices = reviveOverrideData.prices;

                reviveExtraSeconds = reviveOverrideData.sec;
                reviveIAPSeconds = reviveOverrideData.iapSec;
            }

            RewardRemoteConfigData rewardOverrideData = RemoteConfigController.TryGetConfig<RewardRemoteConfigData>("reward");
            if (rewardOverrideData != null)
            {
                CurrencyType rewardCurrencyType;
                if(System.Enum.TryParse(rewardOverrideData.dCurrency, false, out rewardCurrencyType))
                    defaultReward = new CurrencyAmount(rewardCurrencyType, rewardOverrideData.dReward);

                CurrencyType bossCurrencyType;
                if (System.Enum.TryParse(rewardOverrideData.bCurrency, false, out bossCurrencyType))
                    bossReward = new CurrencyAmount(bossCurrencyType, rewardOverrideData.bReward);

                rewardMultiplier = rewardOverrideData.rvMult;

                if (rewardOverrideData.streakBonusPerWin >= 0)
                    winStreakBonusPerWin = rewardOverrideData.streakBonusPerWin;

                if (rewardOverrideData.streakBonusCap >= 0)
                    winStreakBonusCap = rewardOverrideData.streakBonusCap;
            }

            AdsManager.InterstitialConditions += ExtraInterstitialCondition;
        }

        public int GetRevivePrice(int attempt)
        {
            attempt = Mathf.Clamp(attempt, 0, revivePrices.Length - 1);

            return revivePrices[attempt];
        }

        public int GetWinStreakBonus(int streak)
        {
            int winsAfterFirst = Mathf.Max(streak - 1, 0);
            int bonusPerWin = Mathf.Max(winStreakBonusPerWin, 0);
            int bonusCap = Mathf.Max(winStreakBonusCap, 0);

            return Mathf.Min(winsAfterFirst * bonusPerWin, bonusCap);
        }

        private bool ExtraInterstitialCondition()
        {
            return ActiveSession.Current.DisplayLevelIndex >= showAdsAfterLevel;
        }
    }
}
