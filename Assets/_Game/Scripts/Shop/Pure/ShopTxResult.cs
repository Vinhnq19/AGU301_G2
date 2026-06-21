/// <summary>Outcome of a shop Buy/Sell transaction; drives toast feedback.</summary>
public enum ShopTxResult
{
    Success,
    FailedAfford,
    FailedStock,
    FailedNotSellable,
    FailedNoResource,
}
