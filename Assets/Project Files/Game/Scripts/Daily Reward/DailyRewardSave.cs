namespace NebulaSoft
{
    public sealed class DailyRewardSave : ISaveObject
    {
        public string LastClaimedUtcDateKey = string.Empty;
        public int NextDayIndex;
        public int ScheduleVersion = 1;

        public void Flush() { }
    }
}
