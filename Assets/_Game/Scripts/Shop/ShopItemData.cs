using System;
using Unity.Netcode;
using DungeonBuilder.Core.Enums;

[System.Serializable]
public struct ShopItemData : IEquatable<ShopItemData>
{
    // Loại tài nguyên mà item này cung cấp
    public ResourceType ResourceType;
    public int RemainingQuantity;

    public ShopItemData(ResourceType resourceType, int remainingQuantity)
    {
        ResourceType = resourceType;
        RemainingQuantity = remainingQuantity;
    }

    // BẮT BUỘC: NetworkList cần hàm này để so sánh xem Item có bị thay đổi không
    public bool Equals(ShopItemData other)
    {
        return ResourceType == other.ResourceType && 
               RemainingQuantity == other.RemainingQuantity;
    }
}