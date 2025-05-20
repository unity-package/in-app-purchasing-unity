<p align="left">
  <a>
    <img alt="Made With Unity" src="https://img.shields.io/badge/made%20with-Unity-57b9d3.svg?logo=Unity">
  </a>
  <a>
    <img alt="License" src="https://img.shields.io/github/license/unity-package/in-app-purchasing-unity?logo=github">
  </a>
  <a>
    <img alt="Last Commit" src="https://img.shields.io/github/last-commit/unity-package/in-app-purchasing-unity?logo=Mapbox&color=orange">
  </a>
  <a>
    <img alt="Repo Size" src="https://img.shields.io/github/repo-size/unity-package/in-app-purchasing-unity?logo=VirtualBox">
  </a>
  <a>
    <img alt="Last Release" src="https://img.shields.io/github/v/release/unity-package/in-app-purchasing-unity?include_prereleases&logo=Dropbox&color=yellow">
  </a>
</p>

## What
### In-app-purchase support tool for unity games (Unity 2022.3)

## How To Install

### Add the line below to `Packages/manifest.json`

for version `1.0.9`
```json
"com.wolf-org.in-app-purchasing":"https://github.com/unity-package/in-app-purchasing-unity.git#1.0.9",
```
dependency `extensions-unity-1.0.5`
```json
"com.wolf-org.extensions":"https://github.com/unity-package/extensions-unity.git#1.0.5",
```

## Use

### Setup
- Use via MenuItem `Unity-Common` > `IapSettings` or shortcut `Ctrl + W / Command + W` to open `IapSettings`

![Unity_BVkNMfm6fL](https://github.com/wolf-package/in-app-purchasing/assets/102142404/798a790c-c988-48c5-8b32-e88dab94a594)


- If you select `Runtime Auto Init` in `IapSettings` then `IapManager` will be created automatically after loading the scene. Otherwise you will have to attach `IapManager` to the scene to ensure purchases can be made
- Add data product to list (enter id and select product type), then click `Generate Product`
- Select `Validate Product` and enter `Google Play Store Key`, then click `Obfuscator Key`
- Don't forget add Define Symbol `VIRTUESKY_IAP`

- After clicking `Generate Product`, an `IapProduct.cs` script is generated in the following format

```csharp

        public static class IapProduct
	{
	    public const string ID_REMOVEADS = "com.test.removeads";
	    public static IapDataProduct PurchaseRemoveads()
	    {
		return IapManager.PurchaseProduct(IapSettings.Instance.IapDataProducts[0]);
	    }

	    public static bool IsPurchasedRemoveads()
	    {
		return IapManager.IsPurchasedProduct(IapSettings.Instance.IapDataProducts[0]);
	    }

	    public const string ID_1000GEM = "com.test.1000gem";
	    public static IapDataProduct Purchase1000Gem()
	    {
			return IapManager.PurchaseProduct(IapSettings.Instance.IapDataProducts[1]);
	    }

	    public static bool IsPurchased1000Gem()
	    {
		return IapManager.IsPurchasedProduct(IapSettings.Instance.IapDataProducts[1]);
	    }

	}

```

### Handle purchase product
- Example 1:
```csharp
        public Button buttonRemoveAds;
        /// <summary>
        /// refresh ui button remove ads
        /// disable buttonRemoveAds if RemoveAds product has been purchased
        /// </summary>
        void RefreshUI()
        {
            buttonRemoveAds.gameObject.SetActive(!IapProduct.IsPurchasedRemoveads());
        }

        /// <summary>
        /// Buy remove ads iap
        /// </summary>
        public void OnClickRemoveAds()
        {
            IapProduct.PurchaseRemoveads().OnCompleted(() =>
            {
                // handle purchase success
                RefreshUI();
            }).OnFailed(() =>
            {
                // handle purchase failed
            });
        }

```
- Example 2: You create a new script similar to the demo below. then attach it to LoadingScene

```csharp

public class HandlePurchaseIap : MonoBehaviour
{
    private void Awake()
    {
        DontDestroyOnLoad(this.gameObject);
        IapManager.OnPurchaseSucceedEvent += HandlePurchaseSuccess;
        IapManager.OnPurchaseFailedEvent += HandlePurchaseFailed;
    }

    void HandlePurchaseSuccess(string id)
    {
        switch (id)
        {
            case IapProduct.ID_REMOVEADS:
                // handle
                break;
            case IapProduct.ID_1000GEM:
                // handle
                break;
        }
    }

    void HandlePurchaseFailed(string id)
    {
        switch (id)
        {
            case IapProduct.ID_REMOVEADS:
                // handle
                break;
            case IapProduct.ID_1000GEM:
                // handle
                break;
        }
    }
}

```

- Note: Example 1 is typically used to handle the user interface after a successful or failed purchase and cannot handle restore purchase. Therefore, you should use example 2 to handle logic for tasks such as removing ads, unlocking skins,...

- Check to see if the product has been purchased (only applies to Non-Consumable items)
```csharp

    public Button buttonRemoveAds;

    private void OnEnable()
    {
        if (IapProduct.IsPurchasedRemoveads())
        {
            buttonRemoveAds.gameObject.SetActive(false);
        }
    }

```
### Restore purchase
Restore purchase only applies to Non-Consumable items

Restore Purchase is a mandatory feature on iOS to be able to be released to the store.

On Android when you successfully purchased RemoveAds. Then you uninstall your game and reinstall it. If you click buy remove ads again, google play system will report that you already own this item and can't buy it again, now the user has removed ads but the game still displays ads (incorrect). We will need to handle restore purchase of the user's purchased items so that the user avoids this situation.

On Android restore purchase will be called automatically when you reinstall the game via method `ConfirmPendingPurchase` call in `OnInitialized`. On ios you will need to create a restore purchase button for the user to click

When the restore is successful, it will automatically call the successful purchase callback of each item for further processing for the user

```csharp

    public void OnClickRestorePurchase()
    {
#if UNITY_IOS
        IapManager.RestorePurchase();
#endif
    }

```

