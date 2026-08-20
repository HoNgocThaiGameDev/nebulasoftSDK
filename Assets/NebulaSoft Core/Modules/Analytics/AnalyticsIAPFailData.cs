namespace NebulaSoft
{
    public class AnalyticsIAPFailData : IAnalyticsEventData
    {
        public IAPItem Item;
        public NebulaSoft.PurchaseFailureReason FailureReason;
    }
}
