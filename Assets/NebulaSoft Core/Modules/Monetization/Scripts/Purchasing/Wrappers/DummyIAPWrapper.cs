using System.Threading.Tasks;
using UnityEngine;

namespace NebulaSoft
{
    public class DummyIAPWrapper : IAPWrapper
    {
        public override async Task Init(IAPSettings settings)
        {
            await Task.Yield();

            if (Monetization.VerboseLogging)
                Debug.LogWarning("[IAP Manager]: Dummy mode is activated. Configure the module before uploading the game to stores!");

            IAPManager.OnModuleInitialized();
        }

        public override void BuyProduct(ProductKeyType productKeyType)
        {
            if (!IAPManager.IsInitialized)
            {
                SystemMessage.ShowMessage("Network error. Please try again later");

                return;
            }

            IAPItem item = IAPManager.GetIAPItem(productKeyType);

            SystemMessage.ShowLoadingPanel();
            SystemMessage.ChangeLoadingMessage("Payment in progress..");

            Tween.DelayedCall(1.0f, () =>
            {
                if (Monetization.VerboseLogging)
                    Debug.Log(string.Format("[IAPManager]: Purchasing - {0} is completed!", productKeyType));

                IAPManager.OnPurchaseCompleted(item);

                SystemMessage.ChangeLoadingMessage("Payment complete!");
                SystemMessage.HideLoadingPanel();
            }, unscaledTime: true);
        }

        public override ProductData GetProductData(ProductKeyType productKeyType)
        {
            IAPItem iapItem = IAPManager.GetIAPItem(productKeyType);
            if(iapItem != null)
            {
                return new ProductData(iapItem.ProductType);
            }

            return null;
        }

        public override bool IsSubscribed(ProductKeyType productKeyType)
        {
            return false;
        }

        public override bool IsPurchased(string id)
        {
            return false;
        }

        public override void RestorePurchases()
        {
            // DO NOTHING
        }
    }
}