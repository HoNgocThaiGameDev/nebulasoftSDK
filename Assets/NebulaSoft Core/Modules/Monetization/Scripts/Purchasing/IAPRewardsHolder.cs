using UnityEngine;

namespace NebulaSoft
{
    public sealed class IAPRewardsHolder : RewardsHolder
    {
        [Group("Settings")]
        [SerializeField] ProductKeyType productKey;
        public ProductKeyType ProductKey => productKey;

        [Space]
        [Group("Settings")]
        [SerializeField] IAPButton purchaseButton;

        private IAPItem.Save save;
        private ProductData product;

        private void Start()
        {
            InitializeComponents();

            // Link the product type to purchase button
            purchaseButton?.Init(productKey);

            // Wait for IAP Manager initialisation or call method immediately if the module is already Initialized
            IAPManager.SubscribeOnPurchaseModuleInitted(OnIAPManagerLoaded);
        }

        private void OnEnable()
        {
            // Subscribe to purchase callback
            IAPManager.PurchaseCompleted += OnPurchaseComplete;
            IAPManager.PurchasesRestored += OnIAPManagerLoaded;
        }

        private void OnDisable()
        {
            // Unsubscribe from purchase callback
            IAPManager.PurchaseCompleted -= OnPurchaseComplete;
            IAPManager.PurchasesRestored -= OnIAPManagerLoaded;
        }

        private void OnIAPManagerLoaded()
        {
            if (!isInitialized)
                return;

            // Get the product save file to check if it was previously purchased
            // This data is stored only locally so after the game reinstall it will be reset
            save = SaveController.GetSaveObject<IAPItem.Save>($"iap_{productKey}");

            // Get product data wrapper
            // To acess Unity IAP product use product.Product property
            product = IAPManager.GetProductData(productKey);

            // Update button state
            // If there is problem with the internet connection or server didn't return product data loading animation appeared
            purchaseButton.UpdateState(product);

            if (product == null)
                return;

            if (IAPManager.IsPurchased(productKey) || product.ProductType == ProductType.NonConsumable && save.IsPurchased)
            {
                // Disable holder if it's an one time purchase (non-consumable) product 
                if (product.ProductType == ProductType.NonConsumable)
                {
                    // Disable holder game object
                    gameObject.SetActive(false);

                    return;
                }
            }

            // Check if holder needs to be disabled
            if (CheckDisableState())
            {
                // Disable holder game object
                gameObject.SetActive(false);
            }
        }

        private void OnPurchaseComplete(ProductKeyType key)
        {
            if (!isPageActive) return;

            // Check if the purchased product type is equal to holder's product type
            if (productKey == key)
            {
                // Disable holder if it's an one time purchase (non-consumable) product 
                if (product.ProductType == ProductType.NonConsumable)
                {
                    // Disable holder game object
                    gameObject.SetActive(false);
                }
            }

            // Check if holder needs to be disabled
            if (CheckDisableState())
            {
                // Disable holder game object
                gameObject.SetActive(false);
            }
        }
    }
}
