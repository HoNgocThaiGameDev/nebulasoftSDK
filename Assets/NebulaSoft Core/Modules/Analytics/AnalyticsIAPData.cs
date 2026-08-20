namespace NebulaSoft
{
    public class AnalyticsIAPData : IAnalyticsEventData
    {
        public IAPItem Item;

        public string Receipt;
        public float LocalizedPrice;
        public string StoreSpecificId;
        public string IsoCurrencyCode;
    }
}
