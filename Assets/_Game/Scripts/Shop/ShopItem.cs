



using UnityEngine;
[CreateAssetMenu(fileName = "ShopItem", menuName = "ScriptableObjects/ShopItem", order = 1)]
public class ShopItem : ScriptableObject
{
    public string Id;
    public string Name;
    public int Price;

    public bool isUnlimited;

    public CurrencyType CurrencyType;

    public int RemainingQuantity;

    public bool IsSoldOut =>
        RemainingQuantity <= 0 && !isUnlimited;
}