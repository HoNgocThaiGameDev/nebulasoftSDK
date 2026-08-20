namespace NebulaSoft
{
    [System.Serializable]
    public class ReviveRemoteConfigData : RemoteConfigData
    {
        public override string Key => "revive";

        public int sec = 20;
        public int iapSec = 20;

        public string currency = "Coins";
        public int[] prices = new int[] { 900, 1200, 1500 };
    }
}

