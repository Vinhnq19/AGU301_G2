using UnityEngine;
using DungeonBuilder.Core.Enums;

[System.Serializable]
public class ShopPresenter
{
    private readonly ShopView view;
    private readonly ShopModel model;
    private readonly Shop shopNetwork;

    [SerializeField] private CurrencyType currentCurrency = CurrencyType.Coin;

    public ShopPresenter(ShopView view, ShopModel model, Shop shopNetwork = null)
    {
        this.view = view;
        this.model = model;
        this.shopNetwork = shopNetwork;
    
        view.OnTabChanged += HandleTabChanged;

        var items = model.GetItemsByType(currentCurrency);
        view.CreateItemPanels(items, HandleBuyItem);
    }

    public void RefreshShop()
    {
        var items = model.GetItemsByType(currentCurrency);
        view.CreateItemPanels(items, HandleBuyItem);
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

        // Tìm item trong model
        var allItems = model.GetAllItems();
        var item = allItems.Find(x => x.Id == itemId);

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

        // Gởi yêu cầu mua tới Server qua Shop network sử dụng ResourceType
        if (shopNetwork != null)
        {
            shopNetwork.BuyItem(item.ResourceType);
        }
        else
        {
            Debug.LogWarning("Shop network not available");
        }
    }
}