namespace NebulaSoft
{
    [System.Serializable]
    public class RewardRemoteConfigData : RemoteConfigData
    {
        public override string Key => "reward";

        public int dReward = 20;
        public string dCurrency = "Coins";

        public int bReward = 25;
        public string bCurrency = "Coins";

        public int rvMult = 2;

        public int streakBonusPerWin = -1;
        public int streakBonusCap = -1;
    }
}
