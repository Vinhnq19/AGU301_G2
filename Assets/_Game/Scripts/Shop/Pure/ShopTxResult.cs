/// <summary>Outcome of a shop Buy/Sell transaction; drives toast feedback.</summary>
public enum ShopTxResult
{
    Success,
    FailedAfford,
    FailedStock,
    FailedNotSellable,
    FailedNoResource,

    // Item cấu hình sai (vd ResourceType là tiền tệ Coin/Token) — chặn server-side
    // để không thể in tiền (mua/bán item mà "hàng" chính là tiền).
    FailedInvalidItem,

    // Mua upgrade sai thứ tự (vd mua Lv3 khi chưa có Lv2).
    FailedUpgradeOrder,
}
