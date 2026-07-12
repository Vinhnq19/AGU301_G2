




using DungeonBuilder.Core.Enums;
using UnityEngine;
[CreateAssetMenu(fileName = "ShopItem", menuName = "ScriptableObjects/ShopItem", order = 1)]
public class ShopItem : ScriptableObject
{
    public Sprite Icon;
    public string Id;
    public string Name;
    public int Price;

    public int Sell;
    public bool isUnlimited;

    public bool isSellable;

    public CurrencyType CurrencyType;

    public int RemainingQuantity;

    public ResourceType ResourceType;

    /// <summary>
    /// Level đích của item nâng cấp (Lv2 = 2, Lv3 = 3). 0 = không phải item theo level.
    /// Server dùng để bắt buộc mua đúng thứ tự: chỉ mua được khi skill hiện tại == upgradeLevel - 1.
    /// </summary>
    public int upgradeLevel;

    public bool IsSoldOut =>
        RemainingQuantity <= 0 && !isUnlimited;

    /// <summary>
    /// Item nâng cấp kỹ năng (Foraging/Mining Skill): chỉ có 1 nút "Update" để nâng cấp,
    /// KHÔNG mua/bán như tài nguyên thường. Nhận diện tự động theo ResourceType nên
    /// không cần tick tay trên từng SO.
    /// </summary>
    public bool IsUpgrade =>
        ResourceType == ResourceType.MiningSkill ||
        ResourceType == ResourceType.ForgingSkill;
}