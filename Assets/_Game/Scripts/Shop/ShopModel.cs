using System.Collections.Generic;

[System.Serializable]
public class ShopModel
{
    public List<ShopItem> items;

    public List<ShopItem> GetItems(CurrencyType type)
    {
        return items.FindAll(x => x.CurrencyType == type);
    }
}