#if VIRTUESKY_IAP
using System;
using Unity.Services.Core;
using Unity.Services.Core.Environments;
using UnityEngine;
using UnityEngine.Purchasing;
using UnityEngine.Purchasing.Extension;
using VirtueSky.Misc;

namespace VirtueSky.Iap
{
    public class IapManager : MonoBehaviour, IDetailedStoreListener
    {
        [SerializeField] private bool dontDestroyOnLoad = true;
        private static IapManager _instance;
        public static event Action<string> OnPurchaseSucceedEvent;
        public static event Action<string> OnPurchaseFailedEvent;
        public static event Action<Product> OnIapTrackingRevenueEvent;
        public static event Action<bool> OnShowIapNativePopupEvent;

        private IStoreController _controller;
        private IExtensionProvider _extensionProvider;
        private bool IsInitialized { get; set; }
        private IapSettings iapSettings;

        private void Awake()
        {
            if (dontDestroyOnLoad)
            {
                DontDestroyOnLoad(this.gameObject);
            }

            if (_instance == null)
            {
                _instance = this;
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }

        private void Start()
        {
            iapSettings = IapSettings.Instance;
            Init();
        }

        private async void Init()
        {
            var options = new InitializationOptions().SetEnvironmentName("production");
            await UnityServices.InitializeAsync(options);
            InitImpl();
        }

        void InitImpl()
        {
            if (IsInitialized) return;
            var builder = ConfigurationBuilder.Instance(StandardPurchasingModule.Instance());
            RequestProductData(builder);
            builder.Configure<IGooglePlayConfiguration>();

            UnityPurchasing.Initialize(this, builder);
            IsInitialized = true;
        }


        private void PurchaseProductInternal(IapDataProduct product)
        {
#if (UNITY_ANDROID || UNITY_IOS) && !UNITY_EDITOR
            _controller?.InitiatePurchase(product.Id);
#elif UNITY_EDITOR
            InternalPurchaseSuccess(product.Id);
#endif
        }

        private void RequestProductData(ConfigurationBuilder builder)
        {
            foreach (var p in iapSettings.IapDataProducts)
            {
                builder.AddProduct(p.Id, ConvertProductType(p.iapProductType));
            }
        }

        private void InternalPurchaseFailed(string id)
        {
            OnShowIapNativePopupEvent?.Invoke(false);
            foreach (var product in iapSettings.IapDataProducts)
            {
                if (product.Id != id) continue;
                OnPurchaseFailedEvent?.Invoke(product.Id);
                Common.CallActionAndClean(ref product.purchaseFailedCallback);
            }
        }

        public void OnInitializeFailed(InitializationFailureReason error)
        {
            switch (error)
            {
                case InitializationFailureReason.AppNotKnown:
                    Debug.LogError("Is your App correctly uploaded on the relevant publisher console?");
                    break;
                case InitializationFailureReason.PurchasingUnavailable:
                    Debug.LogWarning("In App Purchases disabled in device settings!");
                    break;
                case InitializationFailureReason.NoProductsAvailable:
                    Debug.LogWarning("No products available for purchase!");
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(error), error, null);
            }
        }

        public void OnInitializeFailed(InitializationFailureReason error, string message)
        {
            OnInitializeFailed(error);
        }

        public PurchaseProcessingResult ProcessPurchase(PurchaseEventArgs purchaseEvent)
        {
            if (iapSettings.IsValidatePurchase)
            {
                bool validatedPurchase = true;
#if (UNITY_ANDROID || UNITY_IOS || UNITY_STANDALONE_OSX) && !UNITY_EDITOR
            var validator =
                new UnityEngine.Purchasing.Security.CrossPlatformValidator(UnityEngine.Purchasing.Security.GooglePlayTangle.Data(),
                    UnityEngine.Purchasing.Security.AppleTangle.Data(), Application.identifier);

            try
            {
                // On Google Play, result has a single product ID.
                // On Apple stores, receipts contain multiple products.
                var result = validator.Validate(purchaseEvent.purchasedProduct.receipt);
                Debug.Log("Receipt is valid");
            }
            catch (UnityEngine.Purchasing.Security.IAPSecurityException)
            {
                Debug.Log("Invalid receipt, not unlocking content");
                validatedPurchase = false;
            }
#endif
                if (validatedPurchase) PurchaseVerified(purchaseEvent);
            }
            else
            {
                PurchaseVerified(purchaseEvent);
            }

            return PurchaseProcessingResult.Complete;
        }

        void PurchaseVerified(PurchaseEventArgs purchaseEvent)
        {
            OnShowIapNativePopupEvent?.Invoke(false);
            OnIapTrackingRevenueEvent?.Invoke(purchaseEvent.purchasedProduct);
            InternalPurchaseSuccess(purchaseEvent.purchasedProduct.definition.id);
        }

        void InternalPurchaseSuccess(string id)
        {
            foreach (var product in iapSettings.IapDataProducts)
            {
                if (product.Id != id) continue;
                OnPurchaseSucceedEvent?.Invoke(product.Id);
                Common.CallActionAndClean(ref product.purchaseSuccessCallback);
            }
        }

        public void OnPurchaseFailed(Product product, PurchaseFailureReason failureReason)
        {
            InternalPurchaseFailed(product.definition.id);
        }

        public void OnInitialized(IStoreController controller, IExtensionProvider extensions)
        {
            _controller = controller;
            _extensionProvider = extensions;

#if UNITY_ANDROID && !UNITY_EDITOR
            foreach (var product in _controller.products.all)
            {
                if (product != null && !string.IsNullOrEmpty(product.transactionID))
                    _controller.ConfirmPendingPurchase(product);
            }
#endif
        }

        public void OnPurchaseFailed(Product product, PurchaseFailureDescription failureDescription)
        {
            InternalPurchaseFailed(product.definition.id);
        }

#if UNITY_IOS
        public void RestorePurchase()
        {
            if (!IsInitialized)
            {
                Debug.Log("Restore purchases fail. not initialized!");
                return;
            }

            if (Application.platform == RuntimePlatform.IPhonePlayer || Application.platform == RuntimePlatform.OSXPlayer)
            {
                Debug.Log("Restore purchase started ...");

                var storeProvider = _extensionProvider.GetExtension<IAppleExtensions>();
                storeProvider.RestoreTransactions(_ =>
                {
                    // no purchase are avaiable to restore
                    Debug.Log("Restore purchase continuting: " + _ + ". If no further messages, no purchase available to restore.");
                });
            }
            else
            {
                Debug.Log("Restore purchase fail. not supported on this platform. current = " + Application.platform);
            }
        }
#endif

        ProductType ConvertProductType(IapProductType iapProductType)
        {
            switch (iapProductType)
            {
                case IapProductType.Consumable:
                    return ProductType.Consumable;
                case IapProductType.NonConsumable:
                    return ProductType.NonConsumable;
                case IapProductType.Subscription:
                    return ProductType.Subscription;
            }

            return ProductType.Consumable;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoInitialize()
        {
            if (IapSettings.Instance == null) return;
            if (IapSettings.Instance.RuntimeAutoInit)
            {
                var iapManager = new GameObject("IapManager");
                iapManager.AddComponent<IapManager>();
                DontDestroyOnLoad(iapManager);
            }
        }

        #region Internal API

        private IapDataProduct InternalPurchaseProduct(string id)
        {
            OnShowIapNativePopupEvent?.Invoke(true);
            var product = iapSettings.GetIapProduct(id);
            PurchaseProductInternal(product);
            return product;
        }

        private IapDataProduct InternalPurchaseProduct(IapDataProduct product)
        {
            OnShowIapNativePopupEvent?.Invoke(true);
            PurchaseProductInternal(product);
            return product;
        }

        private bool InternalIsPurchasedProduct(IapDataProduct product)
        {
            if (_controller == null) return false;
            return ConvertProductType(product.iapProductType) == ProductType.NonConsumable &&
                   _controller.products.WithID(product.Id).hasReceipt;
        }

        private bool InternalIsPurchasedProduct(string id)
        {
            if (_controller == null) return false;
            return ConvertProductType(iapSettings.GetIapProduct(id).iapProductType) == ProductType.NonConsumable &&
                   _controller.products.WithID(id).hasReceipt;
        }

        private string InternalLocalizedPriceProduct(IapDataProduct product)
        {
            if (_controller == null) return "";
            return _controller.products.WithID(product.Id).metadata.localizedPriceString;
        }

        private string InternalLocalizedPriceProduct(string id)
        {
            if (_controller == null) return "";
            return _controller.products.WithID(id).metadata.localizedPriceString;
        }

        #endregion

        #region Public API

        public static IapDataProduct PurchaseProduct(string id) => _instance.InternalPurchaseProduct(id);

        public static IapDataProduct PurchaseProduct(IapDataProduct product) =>
            _instance.InternalPurchaseProduct(product);

        public static bool IsPurchasedProduct(IapDataProduct product) => _instance.InternalIsPurchasedProduct(product);
        public static bool IsPurchasedProduct(string id) => _instance.InternalIsPurchasedProduct(id);

        public static string LocalizedPriceProduct(IapDataProduct product) =>
            _instance.InternalLocalizedPriceProduct(product);

        public static string LocalizedPriceProduct(string id) => _instance.InternalLocalizedPriceProduct(id);

        #endregion
    }
}

#endif