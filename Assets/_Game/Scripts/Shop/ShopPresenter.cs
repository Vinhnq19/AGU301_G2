using UnityEngine;
using DungeonBuilder.Core.Enums;

[System.Serializable]
public class ShopPresenter
{
    private readonly ShopView view;
    private readonly ShopModel model;
    private readonly Shop shopNetwork;

    [SerializeField] private CurrencyType currentCurrency = CurrencyType.Coin;

    /// <summary>Cap số lượng mua khi item unlimited (vì không có stock để clamp).</summary>
    private const int UnlimitedBuyMax = 9999;

    public ShopPresenter(ShopView view, ShopModel model, Shop shopNetwork = null)
    {
        this.view = view;
        this.model = model;
        this.shopNetwork = shopNetwork;

        view.OnTabChanged += HandleTabChanged;

        RefreshShop();
    }

    public void RefreshShop()
    {
        var items = model.GetItemsByType(currentCurrency);
        int ownedCurrency = shopNetwork != null
            ? shopNetwork.GetResourceAmount(currentCurrency.ToResourceType())
            : 0;
        view.CreateItemPanels(items, ownedCurrency, HandleBuyItem, HandleSellItem);
    }

    private void HandleTabChanged(CurrencyType type)
    {
        currentCurrency = type;

        Debug.Log($"Presenter received tab: {type}");

        RefreshShop();
    }

    private void HandleBuyItem(string itemId)
    {
        Debug.Log($"Attempting to buy item: {itemId}");

        var item = FindItem(itemId);
        if (item == null)
        {
            Debug.LogWarning($"Item not found: {itemId}");
            return;
        }

        if (item.IsSoldOut)
        {
            Debug.LogWarning($"Item is sold out: {itemId}");
            return;
        }

        // Max = stock còn lại; item unlimited → giới hạn bằng cap an toàn
        int stockMax = item.isUnlimited ? UnlimitedBuyMax : item.RemainingQuantity;
        if (stockMax <= 0)
        {
            Debug.LogWarning($"Cannot buy — no stock: {itemId}");
            return;
        }

        int owned = shopNetwork != null
            ? shopNetwork.GetResourceAmount(item.CurrencyType.ToResourceType())
            : 0;
        int maxQty = ShopMath.MaxBuyQty(stockMax, owned, item.Price);
        if (maxQty <= 0)
        {
            // Can't afford even one. Buy button should already be disabled; this guards races.
            return;
        }

        // Mở popup nhập số lượng; khi xác nhận → DoTransaction (Buy).
        // Popup tự clamp số lượng về [1, maxQty].
        view.ShowQuantityPopup(item.Name, item.Icon, ShopAction.Buy, maxQty, item.Price,
            qty =>
            {
                DoTransaction(item, ShopAction.Buy, qty);
                // Mua het stock remaining -> item sold out -> dong popup (khong mua them duoc).
                if (!item.isUnlimited && qty >= item.RemainingQuantity)
                {
                    view.HideQuantityPopup();
                }
            });
    }

    private void HandleSellItem(string itemId)
    {
        Debug.Log($"Attempting to sell item: {itemId}");

        var item = FindItem(itemId);
        if (item == null)
        {
            Debug.LogWarning($"Item not found: {itemId}");
            return;
        }

        // Presenter-side guard: tránh mở popup nếu item không sellable
        // (View đã disable button nhưng check thêm để chắc)
        if (!item.isSellable)
        {
            Debug.LogWarning($"Item is not sellable: {itemId}");
            return;
        }

        // Max = số resource player đang có (để clamp ô nhập).
        // Server vẫn guard lại bằng TrySpend (atomic).
        int maxQty = shopNetwork != null ? shopNetwork.GetResourceAmount(item.ResourceType) : 0;
        if (maxQty <= 0)
        {
            Debug.LogWarning($"Cannot sell — player owns 0 of {item.ResourceType}");
            return;
        }

        // Mở popup nhập số lượng; khi xác nhận → DoTransaction (Sell).
        view.ShowQuantityPopup(item.Name, item.Icon, ShopAction.Sell, maxQty, item.Sell,
            qty => DoTransaction(item, ShopAction.Sell, qty));
    }

    /// <summary>Thực hiện giao dịch Buy/Sell với số lượng đã chọn, gửi lên Server.</summary>
    private void DoTransaction(ShopItem item, ShopAction action, int qty)
    {
        if (qty <= 0)
            return;

        if (shopNetwork == null)
        {
            Debug.LogWarning("Shop network not available");
            return;
        }

        if (action == ShopAction.Buy)
        {
            Debug.Log($"Buying {qty}x {item.Id}");
            shopNetwork.BuyItem(item.ResourceType, qty);
        }
        else
        {
            Debug.Log($"Selling {qty}x {item.Id}");
            shopNetwork.SellItem(item.ResourceType, qty);
        }
    }

    /// <summary>Called (via Shop feedback RPC) on the requester's client to show a toast.</summary>
    public void ShowFeedback(ShopTxResult result, ResourceType gainedType, int gainedAmt, ResourceType spentType, int spentAmt)
    {
        var feedback = ShopFeedbackFormat.Format(
            result,
            gainedType.ToString(),
            gainedAmt,
            spentType.ToString(),
            spentAmt);
        view.ShowToast(feedback.Message, feedback.Success);
        
        if (DungeonBuilder.Audio.AudioManager.Instance != null)
        {
            if (result == ShopTxResult.Success)
            {
                DungeonBuilder.Audio.AudioManager.Instance.PlaySFX(SoundType.SFX_Get_Coins);
            }
            else
            {
                DungeonBuilder.Audio.AudioManager.Instance.PlaySFX(SoundType.SFX_Error);
            }
        }
    }

    private ShopItem FindItem(string itemId) => model.GetAllItems().Find(x => x.Id == itemId);
}