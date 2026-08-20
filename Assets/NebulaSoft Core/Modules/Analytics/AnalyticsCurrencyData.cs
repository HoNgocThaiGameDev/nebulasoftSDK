using System.Collections.Generic;

namespace NebulaSoft
{
    public class AnalyticsCurrencyData : IAnalyticsEventData
    {
        public string Source;
        public Dictionary<CurrencyType, int> CurrenciesDelta;
    }
}
