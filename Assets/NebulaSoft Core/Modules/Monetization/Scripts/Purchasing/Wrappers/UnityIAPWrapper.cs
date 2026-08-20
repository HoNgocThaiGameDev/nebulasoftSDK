using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using System;

#if MODULE_IAP && !UNITY_IAP_NEW
using UnityEngine.Purchasing;
using UnityEngine.Purchasing.Extension;
using Unity.Services.Core;
using Unity.Services.Core.Environments;
#endif

namespace NebulaSoft
{
    /// <summary>
    /// Wrapper class for Unity IAP functionality.
    /// </summary>
    public class UnityIAPWrapper : IAPWrapper
#if MODULE_IAP && !UNITY_IAP_NEW
        , IDetailedStoreListener
#endif
    {

#if MODULE_IAP && !UNITY_IAP_NEW
        public static IStoreController Controller { get; private set; }
        public static IExtensionProvider Extensions { get; private set; }
        
        private List<PurchaseCallback> purchaseCallbacks = new List<PurchaseCallback>();
#endif

        /// <summary>
        /// Initializes the IAP system with the provided settings.
        /// </summary>
        /// <param name="settings">The IAP settings to use for initialization.</param>
        public override async Task Init(IAPSettings settings)
        {
#if MODULE_IAP && !UNITY_IAP_NEW
            try
            {
                var options = new InitializationOptions().SetEnvironmentName("production");

                await UnityServices.InitializeAsync(options);

                StandardPurchasingModule purchasingModule = StandardPurchasingModule.Instance();

                if (settings.UseFakeStore)
                {
                    purchasingModule.useFakeStoreAlways = true;
                    purchasingModule.useFakeStoreUIMode = (FakeStoreUIMode)settings.FakeStoreMode;
                }

                // Initialize products
                ConfigurationBuilder builder = ConfigurationBuilder.Instance(purchasingModule);

                IAPItem[] items = settings.StoreItems;
                foreach (var item in items)
                {
                    if (!string.IsNullOrEmpty(item.ID))
                    {
                        builder.AddProduct(item.ID, (UnityEngine.Purchasing.ProductType)item.ProductType);
                    }
                    else
                    {
                        Debug.LogWarning($"[IAP Manager]: Product {item.ProductType} does not have configured IDs.");
                    }
                }

                UnityPurchasing.Initialize(this, builder);
            }
            catch (System.Exception exception)
            {
                Debug.LogError($"[IAP Manager]: Initialization failed with exception: {exception}");
            }
#else
            await Task.Run(() => Debug.Log("[IAP Manager]: Define MODULE_IAP is disabled!"));
#endif
        }

#if MODULE_IAP && !UNITY_IAP_NEW
        /// <summary>
        /// Called when Unity IAP is successfully initialized.
        /// </summary>
        /// <param name="controller">The store controller.</param>
        /// <param name="extensions">The extension provider.</param>
        public void OnInitialized(IStoreController controller, IExtensionProvider extensions)
        {
            UnityIAPWrapper.Controller = controller;
            UnityIAPWrapper.Extensions = extensions;

            IAPManager.OnModuleInitialized();
        }

        /// <summary>
        /// Called when Unity IAP initialization fails.
        /// </summary>
        /// <param name="error">The reason for the failure.</param>
        public void OnInitializeFailed(InitializationFailureReason error)
        {
            if (Monetization.VerboseLogging)
                Debug.Log($"[IAPManager]: Module initialization failed with reason: {error}");
        }

        /// <summary>
        /// Called when Unity IAP initialization fails with a message.
        /// </summary>
        /// <param name="error">The reason for the failure.</param>
        /// <param name="message">The failure message.</param>
        public void OnInitializeFailed(InitializationFailureReason error, string message)
        {
            if (Monetization.VerboseLogging)
                Debug.Log($"[IAPManager]: Module initialization failed with reason: {error}, message: {message}");
        }

        /// <summary>
        /// Processes a successful purchase.
        /// </summary>
        /// <param name="e">The purchase event arguments.</param>
        /// <returns>The result of the purchase processing.</returns>
        public PurchaseProcessingResult ProcessPurchase(PurchaseEventArgs e)
        {
            Product product = e.purchasedProduct;

            if (Monetization.VerboseLogging)
                Debug.Log($"[IAPManager]: Purchasing - {product.definition.id} is completed!");

            IAPItem item = IAPManager.GetIAPItem(product.definition.id);
            if (item != null)
            {
                int callbackIndex = purchaseCallbacks.FindIndex(x => x.ProductKeyType == item.ProductKeyType);
                if (callbackIndex != -1)
                {
                    PurchaseCallback callback = purchaseCallbacks[callbackIndex];

                    callback.Callback?.Invoke(new AnalyticsIAPData()
                    {
                        Item = item,

                        Receipt = product.receipt,
                        StoreSpecificId = product.definition.storeSpecificId,
                        IsoCurrencyCode = product.metadata.isoCurrencyCode,
                        LocalizedPrice = (float)product.metadata.localizedPrice,
                    });

                    purchaseCallbacks.RemoveAt(callbackIndex);
                }

                IAPManager.OnPurchaseCompleted(item);
            }
            else
            {
                if (Monetization.VerboseLogging)
                    Debug.Log($"[IAPManager]: Product - {product.definition.id} can't be found!");
            }

            SystemMessage.ChangeLoadingMessage("Payment complete!");
            SystemMessage.HideLoadingPanel();

            return PurchaseProcessingResult.Complete;
        }

        /// <summary>
        /// Called when a purchase fails.
        /// </summary>
        /// <param name="product">The product that failed to purchase.</param>
        /// <param name="failureReason">The reason for the failure.</param>
        public void OnPurchaseFailed(UnityEngine.Purchasing.Product product, UnityEngine.Purchasing.PurchaseFailureReason failureReason)
        {
            if (Monetization.VerboseLogging)
            {
                Debug.Log($"[IAPManager]: Purchasing - {product.definition.id} failed with reason: {failureReason}");
            }

            IAPItem item = IAPManager.GetIAPItem(product.definition.id);
            if (item != null)
            {
                int callbackIndex = purchaseCallbacks.FindIndex(x => x.ProductKeyType == item.ProductKeyType);
                if (callbackIndex != -1)
                    purchaseCallbacks.RemoveAt(callbackIndex);

                NebulaSoft.PurchaseFailureReason purchaseFailureReason = (NebulaSoft.PurchaseFailureReason)failureReason;

                AnalyticsController.OnIAPFailed(item, purchaseFailureReason);

                IAPManager.OnPurchaseFailed(item.ProductKeyType, purchaseFailureReason);
            }
            else
            {
                if (Monetization.VerboseLogging)
                    Debug.Log($"[IAPManager]: Product - {product.definition.id} can't be found!");
            }

            SystemMessage.ChangeLoadingMessage("Payment failed!");
            SystemMessage.HideLoadingPanel();
        }

        /// <summary>
        /// Called when a purchase fails with a description.
        /// </summary>
        /// <param name="product">The product that failed to purchase.</param>
        /// <param name="failureDescription">The failure description.</param>
        public void OnPurchaseFailed(UnityEngine.Purchasing.Product product, PurchaseFailureDescription failureDescription)
        {
            if (Monetization.VerboseLogging)
            {
                Debug.Log($"[IAPManager]: Purchasing - {product.definition.id} failed with reason: {failureDescription.message}");
            }

            IAPItem item = IAPManager.GetIAPItem(product.definition.id);
            if (item != null)
            {
                int callbackIndex = purchaseCallbacks.FindIndex(x => x.ProductKeyType == item.ProductKeyType);
                if (callbackIndex != -1)
                    purchaseCallbacks.RemoveAt(callbackIndex);

                NebulaSoft.PurchaseFailureReason purchaseFailureReason = (NebulaSoft.PurchaseFailureReason)failureDescription.reason;

                AnalyticsController.OnIAPFailed(item, purchaseFailureReason);

                IAPManager.OnPurchaseFailed(item.ProductKeyType, purchaseFailureReason);
            }
            else
            {
                if (Monetization.VerboseLogging)
                    Debug.Log($"[IAPManager]: Product - {product.definition.id} can't be found!");
            }

            SystemMessage.ChangeLoadingMessage("Payment failed!");
            SystemMessage.HideLoadingPanel();
        }
#endif

        /// <summary>
        /// Restores previously purchased products.
        /// </summary>
        public override void RestorePurchases()
        {
#if MODULE_IAP && !UNITY_IAP_NEW
            if (!IAPManager.IsInitialized)
            {
                SystemMessage.ShowMessage("Network error. Please try again later");
                return;
            }

            SystemMessage.ShowLoadingPanel();
            SystemMessage.ChangeLoadingMessage("Restoring purchased products..");

#if UNITY_ANDROID
            Extensions.GetExtension<IGooglePlayStoreExtensions>().RestoreTransactions(OnRestored);
#elif UNITY_IOS
            Extensions.GetExtension<IAppleExtensions>().RestoreTransactions(OnRestored);
#endif
#endif

#if UNITY_EDITOR
            OnRestored(true, "");
#endif
        }

        private void OnRestored(bool result, string error)
        {
#if MODULE_IAP && !UNITY_IAP_NEW
            Tween.DelayedCall(0.5f, () =>
            {
                if (result)
                {
                    SystemMessage.ChangeLoadingMessage("Restoration completed!");
                }
                else
                {
                    SystemMessage.ChangeLoadingMessage($"Restoration failed with error: {error}!");
                }

                Tween.DelayedCall(0.5f, () =>
                {
                    SystemMessage.HideLoadingPanel();
                }, unscaledTime: true);
            }, unscaledTime: true);
#endif
        }

        /// <summary>
        /// Initiates the purchase of a product.
        /// </summary>
        /// <param name="productKeyType">The key type of the product to purchase.</param>
        public override void BuyProduct(ProductKeyType productKeyType)
        {
#if MODULE_IAP && !UNITY_IAP_NEW
            if (!IAPManager.IsInitialized)
            {
                SystemMessage.ShowMessage("Network error. Please try again later");
                return;
            }

            SystemMessage.ShowLoadingPanel();
            SystemMessage.ChangeLoadingMessage("Payment in progress..");

            IAPItem item = IAPManager.GetIAPItem(productKeyType);
            if (item != null)
            {
                AnalyticsController.OnIAPClicked(item); 
                
                for (int i = purchaseCallbacks.Count - 1; i >= 0; i--)
                {
                    if (purchaseCallbacks[i].ProductKeyType == productKeyType)
                    {
                        purchaseCallbacks.RemoveAt(i);
                    }
                }

                purchaseCallbacks.Add(new PurchaseCallback(productKeyType, (iapData) =>
                {
                    AnalyticsController.OnIAPPurchased(iapData);
                }));

                Controller.InitiatePurchase(item.ID);
            }
#else
            SystemMessage.ShowMessage("Network error.");
#endif
        }

        /// <summary>
        /// Gets the product data for a specified product key type.
        /// </summary>
        /// <param name="productKeyType">The key type of the product.</param>
        /// <returns>The product data.</returns>
        public override ProductData GetProductData(ProductKeyType productKeyType)
        {
            if (!IAPManager.IsInitialized)
                return null;

#if MODULE_IAP && !UNITY_IAP_NEW
            IAPItem item = IAPManager.GetIAPItem(productKeyType);
            if (item != null)
            {
                return new ProductData(Controller.products.WithID(item.ID));
            }
#endif

            return null;
        }

        /// <summary>
        /// Checks if a product is subscribed.
        /// </summary>
        /// <param name="productKeyType">The key type of the product.</param>
        /// <returns>True if the product is subscribed, otherwise false.</returns>
        public override bool IsSubscribed(ProductKeyType productKeyType)
        {
#if MODULE_IAP && !UNITY_IAP_NEW
            IAPItem item = IAPManager.GetIAPItem(productKeyType);
            if (item != null)
            {
                Product product = Controller.products.WithID(item.ID);
                if (product != null)
                {
                    if (product.receipt == null)
                        return false;

                    SubscriptionManager subscriptionManager = new SubscriptionManager(product, null);
                    SubscriptionInfo info = subscriptionManager.getSubscriptionInfo();

                    return info.isSubscribed() == Result.True;
                }
            }
#endif

            return false;
        }

        public override bool IsPurchased(string id)
        {
#if MODULE_IAP && !UNITY_IAP_NEW
            IAPItem item = IAPManager.GetIAPItem(id);
            if (item != null)
            {
                Product product = Controller.products.WithID(item.ID);
                if (product != null)
                {
                    return product.hasReceipt;
                }
            }
#endif

            return false;
        }

#if MODULE_IAP && !UNITY_IAP_NEW
        private class PurchaseCallback
        {
            public ProductKeyType ProductKeyType { get; private set; }
            public PurchaseCallbackDelegate Callback { get; private set; }

            public PurchaseCallback(ProductKeyType productKeyType, PurchaseCallbackDelegate callback)
            {
                ProductKeyType = productKeyType;
                Callback = callback;
            }

            public delegate void PurchaseCallbackDelegate(AnalyticsIAPData iapData);
        }
#endif
    }
}