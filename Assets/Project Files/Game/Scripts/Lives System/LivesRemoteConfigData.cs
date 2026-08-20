namespace NebulaSoft
{
    [System.Serializable]
    public class LivesRemoteConfigData : RemoteConfigData
    {
        public override string Key => "lives";

        public int maxCount = 5;
        public int resetTime = 1200;

        public string currency = "Coins";
        public int price = 900;
    }
}

