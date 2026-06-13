using UnityEngine;

public class ShopPresenter
{
    private readonly ShopView view;
    private readonly ShopModel model;

    private CurrencyType currentCurrency = CurrencyType.Coin;

    public ShopPresenter(ShopView view, ShopModel model)
    {
        this.view = view;
        this.model = model;

        view.OnTabChanged += HandleTabChanged;
    }

    private void HandleTabChanged(CurrencyType type)
    {
        currentCurrency = type;

        Debug.Log($"Presenter received tab: {type}");

        RefreshShop();
    }

    private void RefreshShop()
    {
        var items = model.GetItems(currentCurrency);

        // push data back to view
        // view.ShowItems(items);
    }
}