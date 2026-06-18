using System;
using Unity.Collections; // Chứa FixedString
using Unity.Netcode;

[System.Serializable]
public struct ShopItemData : IEquatable<ShopItemData>
{
    // THAY ĐỔI: Dùng FixedString thay cho string
    // FixedString32Bytes hỗ trợ chuỗi dài tối đa 32 bytes (rất phù hợp cho ID)
    public FixedString32Bytes ItemId; 
    public int RemainingQuantity;

    public ShopItemData(FixedString32Bytes itemId, int remainingQuantity)
    {
        ItemId = itemId;
        RemainingQuantity = remainingQuantity;
    }

    // BẮT BUỘC: NetworkList cần hàm này để so sánh xem Item có bị thay đổi không
    public bool Equals(ShopItemData other)
    {
        return ItemId == other.ItemId && 
               RemainingQuantity == other.RemainingQuantity;
    }
}