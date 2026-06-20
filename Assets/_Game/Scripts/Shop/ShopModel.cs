using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class ShopModel
{
    public List<ShopItem> items;

    public List<ShopItem> GetItemsByType(CurrencyType type)
    {
        return items.FindAll(x => x.CurrencyType == type);
    }

    public List<ShopItem> GetAllItems()
    {
        return items;
    }

}