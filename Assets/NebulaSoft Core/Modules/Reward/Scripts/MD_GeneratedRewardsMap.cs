// Auto-generated file. Do not edit.
using System;
using System.Collections.Generic;

namespace NebulaSoft
{
    public static class RewardsMap
    {
        public static Dictionary<Type, Type> ViewMap { get; } = GetMap();

        public static Dictionary<Type, Type> GetMap()
        {
            Dictionary<Type, Type> map = new Dictionary<Type, Type>();
            map[typeof(NebulaSoft.LivesReward)] = typeof(NebulaSoft.LivesRewardView);
            map[typeof(NebulaSoft.LivesMaxLivesReward)] = typeof(NebulaSoft.LivesRewardView);
            map[typeof(NebulaSoft.NoAdsReward)] = typeof(NebulaSoft.NoAdsRewardView);
            map[typeof(NebulaSoft.CurrencyReward)] = typeof(NebulaSoft.CurrencyRewardView);
            map[typeof(NebulaSoft.SkinReward)] = typeof(NebulaSoft.SkinRewardView);
            map[typeof(NebulaSoft.LivesInfiniteModeReward)] = typeof(NebulaSoft.LivesInfiniteModeRewardView);

            return map;
        }
    }
}
