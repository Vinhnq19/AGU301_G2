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

        var items = model.GetItemsByType(currentCurrency);
        view.CreateItemPanels(items, HandleBuyItem, HandleSellItem);
    }

    public void RefreshShop()
    {
        var items = model.GetItemsByType(currentCurrency);
        view.CreateItemPanels(items, HandleBuyItem, HandleSellItem);
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
        int maxQty = item.isUnlimited ? UnlimitedBuyMax : item.RemainingQuantity;
        if (maxQty <= 0)
        {
            Debug.LogWarning($"Cannot buy — no stock: {itemId}");
            return;
        }

        // Mở popup nhập số lượng; khi xác nhận → DoTransaction (Buy).
        // Popup tự clamp số lượng về [1, maxQty].
        view.ShowQuantityPopup(item.Name, ShopAction.Buy, maxQty, item.Price,
            qty => DoTransaction(item, ShopAction.Buy, qty));
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
        view.ShowQuantityPopup(item.Name, ShopAction.Sell, maxQty, item.Sell,
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

    private ShopItem FindItem(string itemId) => model.GetAllItems().Find(x => x.Id == itemId);
}