using System;
using Unity.Netcode;
using DungeonBuilder.Core.Enums;

[System.Serializable]
public struct ShopItemData : IEquatable<ShopItemData>
{
    // Loại tài nguyên mà item này cung cấp
    public ResourceType ResourceType;
    public int RemainingQuantity;

    // Sell price — server-authoritative, network-replicated (cho phép runtime change: sales, events, ...)
    public int Sell;

    public ShopItemData(ResourceType resourceType, int remainingQuantity, int sell)
    {
        ResourceType = resourceType;
        RemainingQuantity = remainingQuantity;
        Sell = sell;
    }

    // BẮT BUỘC: NetworkList cần hàm này để so sánh xem Item có bị thay đổi không
    public bool Equals(ShopItemData other)
    {
        return ResourceType == other.ResourceType &&
               RemainingQuantity == other.RemainingQuantity &&
               Sell == other.Sell;
    }
}