using System;
using System.Collections.Generic;
using UnityEngine;

namespace NebulaSoft
{
    [Serializable]
    [RegisterReward(typeof(CurrencyRewardView))]
    public sealed class CurrencyReward : Reward
    {
        [SerializeField] CurrencyAmount[] currencies;
        public CurrencyAmount[] Currencies => currencies;

        [Space]
        [SerializeField] string analyticsSource;

        public CurrencyReward() { }
        public CurrencyReward(CurrencyAmount[] currencies, string analyticsSource = "")
        {
            this.currencies = currencies;
            this.analyticsSource = analyticsSource;
        }

        public override void ApplyReward()
        {
            Dictionary<CurrencyType, int> analyticsDictionary = new Dictionary<CurrencyType, int>();

            foreach (CurrencyAmount currency in currencies)
            {
                CurrencyController.Add(currency.CurrencyType, currency.Amount);

                analyticsDictionary.TryAdd(currency.CurrencyType, currency.Amount);
            }

            if (!string.IsNullOrEmpty(analyticsSource))
            {
                AnalyticsController.OnCurrencySource(analyticsSource, analyticsDictionary);
            }
        }

        public int GetAmount(CurrencyType currencyType)
        {
            foreach (CurrencyAmount currency in currencies)
            {
                if (currency.CurrencyType == currencyType)
                    return currency.Amount;
            }

            return 0;
        }

        public override List<IRewardPreview> GetRewardPreviews()
        {
            if(!currencies.IsNullOrEmpty())
            {
                List<IRewardPreview> previews = new List<IRewardPreview>();
                foreach (CurrencyAmount currencyAmount in currencies)
                {
                    Currency currency = currencyAmount.Currency;

                    CurrencyRewardPreviewSettings previewSettings = currency.PreviewSettings;
                    if (previewSettings != null)
                    {
                        previews.Add(previewSettings.GetPreview(currencyAmount));
                    }
                    else
                    {
                        previews.Add(new RewardPreview(currency.Icon, $"{CurrencyHelper.Format(currencyAmount.Amount)}", CurrencyRewardPreviewSettings.DEFAULT_SORING_ORDER));
                    }
                }

                return previews;
            }

            return null;
        }
    }
}
