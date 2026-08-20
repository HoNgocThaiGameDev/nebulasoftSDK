using System.Collections.Generic;
using UnityEngine;

#if MODULE_IAP
using UnityEngine.Purchasing;
#endif

namespace NebulaSoft
{
    [System.Serializable]
    public sealed class IAPPendingTransactionLog
    {
        public string DocumentId;
        public string ProductId;
        public string ProductType;
        public string Platform;
        public string GooglePlayOrderId;
        public string TransactionIdHash;
        public string AppVersion;
        public string ExpectedFirebaseUid;
        public string PlayerNameAtPurchase;
        public int PlayerAvatarAtPurchase;
        public int PlayerFrameAtPurchase;
        public bool HasProfileSnapshot;
        public string ProfileSnapshotSource;
    }

    [System.Serializable]
    public sealed class IAPPendingPurchaseAttemptLog
    {
        public string DocumentId;
        public string ProductId;
        public string ProductType;
        public string Platform;
        public string Status;
        public string FailureReason;
        public string AppVersion;
        public string ExpectedFirebaseUid;
        public string PlayerNameAtAttempt;
        public int PlayerAvatarAtAttempt;
        public int PlayerFrameAtAttempt;
        public bool HasProfileSnapshot;
        public string ProfileSnapshotSource;
    }

    [StaticUnload]
    public static class IAPManager
    {
        private static Dictionary<ProductKeyType, IAPItem> productsTypeToProductLink;

        public static bool IsInitialized { get; private set; } = false;
        public static bool IsRestoreInProgress { get; private set; } = false;

        private static IAPWrapper wrapper;

        public static event SimpleCallback Initialized;
        public static event SimpleCallback PurchasesRestored;
        public static event ProductCallback PurchaseCompleted;
        /// <summary>Raised for a new purchase after its rewards have been applied.</summary>
        public static event System.Action<IAPItem> PurchaseRewardPresentationRequested;
        public static event ProductFailCallback PurchaseFailed;

        private static IAPSettings settings;

        private static Save save;

        public static void Init(MonetizationSettings monetizationSettings)
        {
            if (IsInitialized)
            {
                Debug.LogError("[IAP Manager]: Module is already initialized!");
                return;
            }

            settings = monetizationSettings?.IAPSettings;
            if (settings == null)
            {
                Debug.LogError("[IAP Manager]: IAPSettings is null!");
                return;
            }

            save = SaveController.GetSaveObject<Save>("iapGlobalSave");

            productsTypeToProductLink = new Dictionary<ProductKeyType, IAPItem>();

            IAPItem[] items = settings.StoreItems;
            if (items != null)
            {
                foreach (IAPItem item in items)
                {
                    item.Init();

                    if (!productsTypeToProductLink.ContainsKey(item.ProductKeyType))
                    {
                        productsTypeToProductLink.Add(item.ProductKeyType, item);
                    }
                    else
                    {
                        Debug.LogError($"[IAP Manager]: Product with the type {item.ProductKeyType} has duplicates in the list!", settings);
                    }
                }
            }

            wrapper = GetPlatformWrapper();
            wrapper.Init(settings);

            FirebaseIAPTransactionLogger.TryFlushPendingTransactions();
        }

        public static IAPItem GetIAPItem(string productID)
        {
            if (string.IsNullOrEmpty(productID)) return null;

            foreach (IAPItem item in productsTypeToProductLink.Values)
            {
                if (item.ID == productID)
                    return item;
            }

            return null;
        }

        public static IAPItem GetIAPItem(ProductKeyType productKeyType)
        {
            productsTypeToProductLink.TryGetValue(productKeyType, out IAPItem item);

            return item;
        }

        public static void RestorePurchases()
        {
            if (!Monetization.IsActive)
            {
                SystemMessage.ShowMessage("Purchases are unavailable in this build.");
                return;
            }

            if (!IsInitialized)
            {
                SystemMessage.ShowMessage("Google Play is not connected. Please try again shortly.");
                return;
            }

            if (IsRestoreInProgress)
                return;

            IsRestoreInProgress = true;
            wrapper.RestorePurchases();
        }

        public static void FinishRestorePurchases()
        {
            IsRestoreInProgress = false;
        }

        public static void SubscribeOnPurchaseModuleInitted(SimpleCallback callback)
        {
            if (IsInitialized)
            {
                callback?.Invoke();
            }
            else
            {
                Initialized += callback;
            }
        }

        public static void BuyProduct(ProductKeyType productKeyType)
        {
            if (!Monetization.IsActive)
            {
                Debug.LogWarning("[IAP Manager]: Mobile monetization is disabled!", settings);
                return;
            }

            if (!IsInitialized)
            {
                Debug.LogWarning("[IAP Manager]: The module is not initialized!", settings);
                return;
            }

            wrapper.BuyProduct(productKeyType);
        }

        public static ProductData GetProductData(ProductKeyType productKeyType)
        {
            if (!Monetization.IsActive || !IsInitialized) return new ProductData();

            ProductData product = wrapper.GetProductData(productKeyType);

            if (product == null)
            {
                Debug.LogWarning($"[IAP Manager]: Product of type '{productKeyType}' was not found in Monetization Settings. Please ensure it is added to the products list.", settings);
            }

            return product;
        }

        public static bool IsSubscribed(ProductKeyType productKeyType)
        {
            if (!Monetization.IsActive || !IsInitialized) return false;

            return wrapper.IsSubscribed(productKeyType);
        }

        public static bool IsPurchased(ProductKeyType productKeyType)
        {
#if MODULE_IAP
            IAPItem iapItem = GetIAPItem(productKeyType);
            if(iapItem != null)
            {
                return wrapper.IsPurchased(iapItem.ID);
            }
#endif

            return false;
        }

        public static string GetProductLocalPriceString(ProductKeyType productKeyType)
        {
            var product = GetProductData(productKeyType);

            if (product == null)
            {
                Debug.LogWarning($"[IAP Manager]: Product of type '{productKeyType}' was not found in Monetization Settings. Please ensure it is added to the products list.", settings);
                return string.Empty;
            }

            return product.GetLocalPrice();
        }

        public static void OnModuleInitialized()
        {
            IsInitialized = true;

            if (Initialized != null)
            {
                System.Delegate[] listDelegates = Initialized.GetInvocationList();
                foreach (var d in listDelegates)
                {
                    SimpleCallback cb = (SimpleCallback)d; 

                    if (d.Target is UnityEngine.Object uo && uo == null)
                        continue;

                    try
                    {
                        cb?.Invoke();
                    }
                    catch (System.Exception e)
                    {
                        Debug.LogException(e);
                    }
                }
            }

            if (Monetization.VerboseLogging)
                Debug.Log("[IAPManager]: Module is initialized!");
        }

        public static void OnPurchaseCompleted(IAPItem item)
        {
            if(!save.FirstPurchase)
            {
                AnalyticsController.TrackEvent(AnalyticsEventType.IAPFirstPurchase);

                save.FirstPurchase = true;
            }

            item.OnProductPurchased();

            PurchaseCompleted?.Invoke(item.ProductKeyType);
            PurchaseRewardPresentationRequested?.Invoke(item);
        }

        public static void OnPurchasesRestored()
        {
            PurchasesRestored?.Invoke();
            FirebaseNoAdsEntitlementSync.RestoreForCurrentUser();
        }

        public static void QueuePendingIAPTransaction(IAPPendingTransactionLog transaction)
        {
            if (save == null || transaction == null || string.IsNullOrWhiteSpace(transaction.DocumentId))
                return;

            if (save.PendingTransactions == null)
                save.PendingTransactions = new List<IAPPendingTransactionLog>();

            if (save.PendingTransactions.Exists(entry => entry != null && entry.DocumentId == transaction.DocumentId))
                return;

            save.PendingTransactions.Add(transaction);
            SaveController.MarkAsSaveIsRequired();
            SaveController.Save(true, false);
        }

        public static List<IAPPendingTransactionLog> GetPendingIAPTransactions()
        {
            if (save == null || save.PendingTransactions == null)
                return new List<IAPPendingTransactionLog>();

            return new List<IAPPendingTransactionLog>(save.PendingTransactions);
        }

        public static void MarkIAPTransactionLogged(string documentId)
        {
            if (save == null || save.PendingTransactions == null || string.IsNullOrWhiteSpace(documentId))
                return;

            int removedCount = save.PendingTransactions.RemoveAll(entry => entry != null && entry.DocumentId == documentId);
            if (removedCount > 0)
                SaveController.MarkAsSaveIsRequired();
        }

        public static void QueuePendingIAPAttempt(IAPPendingPurchaseAttemptLog attempt)
        {
            if (save == null || attempt == null || string.IsNullOrWhiteSpace(attempt.DocumentId))
                return;

            if (save.PendingAttempts == null)
                save.PendingAttempts = new List<IAPPendingPurchaseAttemptLog>();

            if (save.PendingAttempts.Exists(entry => entry != null && entry.DocumentId == attempt.DocumentId))
                return;

            save.PendingAttempts.Add(attempt);
            SaveController.MarkAsSaveIsRequired();
            SaveController.Save(true, false);
        }

        public static List<IAPPendingPurchaseAttemptLog> GetPendingIAPAttempts()
        {
            if (save == null || save.PendingAttempts == null)
                return new List<IAPPendingPurchaseAttemptLog>();

            return new List<IAPPendingPurchaseAttemptLog>(save.PendingAttempts);
        }

        public static void MarkIAPAttemptLogged(string documentId)
        {
            if (save == null || save.PendingAttempts == null || string.IsNullOrWhiteSpace(documentId))
                return;

            int removedCount = save.PendingAttempts.RemoveAll(entry => entry != null && entry.DocumentId == documentId);
            if (removedCount > 0)
                SaveController.MarkAsSaveIsRequired();
        }

        public static bool IsIAPTransactionFulfilled(string transactionHash)
        {
            return save != null
                && !string.IsNullOrWhiteSpace(transactionHash)
                && save.FulfilledTransactionHashes != null
                && save.FulfilledTransactionHashes.Contains(transactionHash);
        }

        public static void MarkIAPTransactionFulfilled(string transactionHash)
        {
            if (save == null || string.IsNullOrWhiteSpace(transactionHash))
                return;

            if (save.FulfilledTransactionHashes == null)
                save.FulfilledTransactionHashes = new List<string>();

            if (save.FulfilledTransactionHashes.Contains(transactionHash))
                return;

            save.FulfilledTransactionHashes.Add(transactionHash);

            // Google Play normally does not deliver an acknowledged order again,
            // so retain only a bounded safety window for failed acknowledgements.
            const int maxFulfilledTransactionHashes = 256;
            int overflow = save.FulfilledTransactionHashes.Count - maxFulfilledTransactionHashes;
            if (overflow > 0)
                save.FulfilledTransactionHashes.RemoveRange(0, overflow);

            SaveController.MarkAsSaveIsRequired();
            SaveController.Save(true, false);
        }

        public static bool IsPayableUser()
        {
            if(save != null)
                return save.FirstPurchase;

            return false;
        }

        public static void OnPurchaseFailed(ProductKeyType productKey, NebulaSoft.PurchaseFailureReason failureReason)
        {
            PurchaseFailed?.Invoke(productKey, failureReason);
        }

        private static IAPWrapper GetPlatformWrapper()
        {
#if MODULE_IAP
#if UNITY_IAP_NEW
            return new UnityIAP5Wrapper();
#else
            return new UnityIAPWrapper();
#endif
#else
            return new DummyIAPWrapper();
#endif
        }

        private static void UnloadStatic()
        {
            IsInitialized = false;
            IsRestoreInProgress = false;

            productsTypeToProductLink = null;
            wrapper = null;
            settings = null;

            Initialized = null;
            PurchasesRestored = null;
            PurchaseCompleted = null;
            PurchaseRewardPresentationRequested = null;
            PurchaseFailed = null;
        }

        public delegate void ProductCallback(ProductKeyType productKeyType);
        public delegate void ProductFailCallback(ProductKeyType productKeyType, NebulaSoft.PurchaseFailureReason failureReason);

        [System.Serializable]
        public class Save : ISaveObject
        {
            public bool FirstPurchase = false;
            public List<IAPPendingTransactionLog> PendingTransactions = new List<IAPPendingTransactionLog>();
            public List<IAPPendingPurchaseAttemptLog> PendingAttempts = new List<IAPPendingPurchaseAttemptLog>();
            public List<string> FulfilledTransactionHashes = new List<string>();

            public void Flush()
            {

            }
        }
    }
}
