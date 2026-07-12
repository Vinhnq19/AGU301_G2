/// <summary>Toast payload: text + whether it represents success.</summary>
public readonly struct ShopFeedback
{
    public readonly string Message;
    public readonly bool Success;

    public ShopFeedback(string message, bool success)
    {
        Message = message;
        Success = success;
    }
}

/// <summary>
/// Pure toast-message formatter. Callers pass resolved resource/currency NAMES as
/// strings (no dependency on Assembly-CSharp enums) so this stays unit-testable.
/// On FailedAfford / FailedNoResource, <paramref name="spentName"/> is the name of
/// the resource/currency the player lacked.
/// </summary>
public static class ShopFeedbackFormat
{
    public static ShopFeedback Format(
        ShopTxResult result,
        string gainedName,
        int gainedAmt,
        string spentName,
        int spentAmt)
    {
        switch (result)
        {
            case ShopTxResult.Success:
                return new ShopFeedback($"+{gainedAmt} {gainedName} / -{spentAmt} {spentName}", true);
            case ShopTxResult.FailedAfford:
            case ShopTxResult.FailedNoResource:
                return new ShopFeedback($"Not enough {spentName}", false);
            case ShopTxResult.FailedStock:
                return new ShopFeedback("Sold out", false);
            case ShopTxResult.FailedNotSellable:
                return new ShopFeedback("Cannot sell this item", false);
            case ShopTxResult.FailedInvalidItem:
                return new ShopFeedback("Item cannot be traded", false);
            case ShopTxResult.FailedUpgradeOrder:
                return new ShopFeedback("Buy the previous upgrade first", false);
            default:
                return new ShopFeedback("Transaction failed", false);
        }
    }
}
