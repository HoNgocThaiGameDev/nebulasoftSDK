using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NebulaSoft
{
    public class IAPButton : MonoBehaviour
    {
        [SerializeField] Image backImage;
        [SerializeField] Button button;
        [SerializeField] TMP_Text priceText;
        [SerializeField] GameObject loadingObject;

        [Space]
        [SerializeField] Sprite activeBackSprite;
        [SerializeField] Sprite unactiveBackSprite;

        private ProductKeyType key;

        private void Awake()
        {
            button.onClick.AddListener(OnButtonClicked);
            UIAudioFeedback.RegisterButton(button);
        }

        public void Init(ProductKeyType key)
        {
            this.key = key;

            UpdateState();
        }

        public void UpdateState()
        {
            UpdateState(IAPManager.GetProductData(key));
        }

        public void UpdateState(ProductData product)
        {
            if (loadingObject == null || priceText == null || backImage == null)
            {
                Debug.LogWarning($"[IAPButton] UI references are not assigned. Skipping UpdateState. Key: {key}");

                return;
            }

            if (product != null)
            {
                loadingObject.SetActive(false);
                priceText.gameObject.SetActive(true);

                backImage.sprite = activeBackSprite;

                IAPItem iapItem = IAPManager.GetIAPItem(key);
#if UNITY_EDITOR
                // Unity IAP's Editor store reports a generic $0.01 price for every
                // product. Preview the configured USD catalog price instead.
                if (iapItem != null)
                {
                    priceText.text = GetDefaultUsdPrice(iapItem);
                    return;
                }
#endif

                if (!string.IsNullOrWhiteSpace(product.LocalizedPriceString))
                {
                    priceText.text = product.GetLocalPrice();
                }
                else
                {
                    if(iapItem != null)
                    {
                        // This is a pre-catalog fallback only. A real Google Play
                        // product always replaces it with localizedPriceString.
                        priceText.text = GetDefaultUsdPrice(iapItem);
                    }
                    else
                    {
                        priceText.text = product.GetLocalPrice();
                    }
                }
            }
            else
            {
                SetDisabledState();
            }
        }

        private static string GetDefaultUsdPrice(IAPItem iapItem)
        {
            return string.Format(CultureInfo.InvariantCulture, "${0:0.00}", iapItem.DefaultUSDPrice);
        }

        private void SetDisabledState()
        {
            if (loadingObject != null)
                loadingObject.SetActive(true);

            if (priceText != null)
                priceText.gameObject.SetActive(false);

            if (backImage != null && unactiveBackSprite != null)
                backImage.sprite = unactiveBackSprite;
        }

        private void OnButtonClicked()
        {
            if (LeaderboardBottomNavigationController.IsNoConnectionActive())
            {
                LeaderboardBottomNavigationController.TryShowNoConnectionPopup();
                return;
            }

            IAPManager.BuyProduct(key);
        }
    }
}
