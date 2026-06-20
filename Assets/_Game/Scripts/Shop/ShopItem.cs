




using DungeonBuilder.Core.Enums;
using UnityEngine;
[CreateAssetMenu(fileName = "ShopItem", menuName = "ScriptableObjects/ShopItem", order = 1)]
public class ShopItem : ScriptableObject
{
    public string Id;
    public string Name;
    public int Price;

    public int Sell;
    public bool isUnlimited;

    public bool isSellable;

    public CurrencyType CurrencyType;

    public int RemainingQuantity;

    public ResourceType ResourceType;

    public bool IsSoldOut =>
        RemainingQuantity <= 0 && !isUnlimited;
}