using UnityEngine;

namespace NebulaSoft
{
    [RegisterModule("Daily Reward", core: false)]
    public sealed class DailyRewardInitModule : InitModule
    {
        public override string ModuleName => "Daily Reward";

        [SerializeField] DailyRewardDatabase database;
        public DailyRewardDatabase Database => database;

        public override void CreateComponent()
        {
            DailyRewardService.Init(database);
        }
    }
}
