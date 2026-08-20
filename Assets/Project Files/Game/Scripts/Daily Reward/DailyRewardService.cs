using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace NebulaSoft
{
    [StaticUnload]
    public static class DailyRewardService
    {
        public const string SaveKey = "Daily Reward";
        private const string DateFormat = "yyyy-MM-dd";

        private static IDailyRewardScheduleProvider scheduleProvider;
        private static IDailyRewardClock clock;
        private static IDailyRewardStateStore stateStore;
        private static DailyRewardSave save;

        public static bool IsInitialized => scheduleProvider != null && clock != null && stateStore != null && save != null;
        public static event Action StateChanged;

        public static void Init(DailyRewardDatabase database)
        {
            Init(database, new LocalDailyRewardClock(), new LocalDailyRewardStateStore());
        }

        public static void Init(IDailyRewardScheduleProvider provider, IDailyRewardClock rewardClock, IDailyRewardStateStore rewardStateStore)
        {
            scheduleProvider = provider;
            clock = rewardClock;
            stateStore = rewardStateStore;

            if (scheduleProvider == null || scheduleProvider.Days == null || scheduleProvider.Days.Count != DailyRewardDatabase.CycleLength)
            {
                Debug.LogError("[Daily Reward] A valid seven-day schedule is required.");
                return;
            }

            if (clock == null || stateStore == null || !SaveController.IsSaveLoaded)
            {
                Debug.LogError("[Daily Reward] Save and time services must be ready before initialization.");
                return;
            }

            save = stateStore.Load() ?? new DailyRewardSave();
            save.NextDayIndex = NormalizeDayIndex(save.NextDayIndex);
            save.ScheduleVersion = scheduleProvider.ScheduleVersion;
            NormalizeForCurrentDate();
        }

        public static DailyRewardState GetState()
        {
            if (!IsInitialized)
                return new DailyRewardState(0, -1, false, false);

            NormalizeForCurrentDate();

            string today = GetTodayKey();
            bool claimedToday = string.Equals(save.LastClaimedUtcDateKey, today, StringComparison.Ordinal);
            bool clockMovedBack = IsClockBeforeLastClaim(today);
            bool canClaim = !claimedToday && !clockMovedBack;
            int claimedDayIndex = claimedToday
                ? NormalizeDayIndex(save.NextDayIndex - 1)
                : -1;

            return new DailyRewardState(save.NextDayIndex, claimedDayIndex, canClaim, claimedToday);
        }

        public static DailyRewardDayDefinition GetDay(int dayIndex)
        {
            return !IsInitialized || dayIndex < 0 || dayIndex >= scheduleProvider.Days.Count
                ? null
                : scheduleProvider.Days[dayIndex];
        }

        public static bool TryClaimToday()
        {
            DailyRewardState state = GetState();
            if (!state.CanClaimToday)
                return false;

            DailyRewardDayDefinition day = GetDay(state.CurrentDayIndex);
            if (day == null || day.Rewards == null || day.Rewards.Count == 0)
            {
                Debug.LogWarning("[Daily Reward] Current reward day is not configured.");
                return false;
            }

            for (int i = 0; i < day.Rewards.Count; i++)
            {
                DailyRewardGrant reward = day.Rewards[i];
                if (reward == null || !reward.IsAvailable())
                {
                    Debug.LogWarning("[Daily Reward] A configured reward is not available yet.");
                    return false;
                }
            }

            for (int i = 0; i < day.Rewards.Count; i++)
                day.Rewards[i].Apply("DailyReward");

            save.LastClaimedUtcDateKey = GetTodayKey();
            save.NextDayIndex = NormalizeDayIndex(state.CurrentDayIndex + 1);
            save.ScheduleVersion = scheduleProvider.ScheduleVersion;
            stateStore.Save(save);
            StateChanged?.Invoke();
            return true;
        }

        /// <summary>
        /// Clears the local daily-reward progress so the next claim starts at Day 1.
        /// Intended for development and QA tooling only.
        /// </summary>
        public static bool ResetForTesting()
        {
            if (!IsInitialized)
                return false;

            save.LastClaimedUtcDateKey = string.Empty;
            save.NextDayIndex = 0;
            save.ScheduleVersion = scheduleProvider.ScheduleVersion;
            stateStore.Save(save);
            StateChanged?.Invoke();
            return true;
        }

        private static void NormalizeForCurrentDate()
        {
            if (!IsInitialized || string.IsNullOrEmpty(save.LastClaimedUtcDateKey))
                return;

            DateTime lastClaimedDate;
            if (!DateTime.TryParseExact(save.LastClaimedUtcDateKey, DateFormat, CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out lastClaimedDate))
            {
                save.LastClaimedUtcDateKey = string.Empty;
                save.NextDayIndex = 0;
                stateStore.Save(save);
                return;
            }

            int elapsedDays = (clock.UtcNow.Date - lastClaimedDate.Date).Days;
            if (elapsedDays <= 1)
                return;

            save.NextDayIndex = 0;
            stateStore.Save(save);
        }

        private static bool IsClockBeforeLastClaim(string today)
        {
            if (string.IsNullOrEmpty(save.LastClaimedUtcDateKey))
                return false;

            DateTime todayDate;
            DateTime lastClaimedDate;
            return DateTime.TryParseExact(today, DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out todayDate)
                && DateTime.TryParseExact(save.LastClaimedUtcDateKey, DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out lastClaimedDate)
                && todayDate.Date < lastClaimedDate.Date;
        }

        private static string GetTodayKey()
        {
            return clock.UtcNow.ToString(DateFormat, CultureInfo.InvariantCulture);
        }

        private static int NormalizeDayIndex(int dayIndex)
        {
            int cycleLength = scheduleProvider != null && scheduleProvider.Days != null && scheduleProvider.Days.Count > 0
                ? scheduleProvider.Days.Count
                : DailyRewardDatabase.CycleLength;
            return ((dayIndex % cycleLength) + cycleLength) % cycleLength;
        }

        private static void UnloadStatic()
        {
            scheduleProvider = null;
            clock = null;
            stateStore = null;
            save = null;
            StateChanged = null;
        }
    }
}
