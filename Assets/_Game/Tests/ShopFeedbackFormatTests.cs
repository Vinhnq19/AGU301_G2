using NUnit.Framework;

public class ShopFeedbackFormatTests
{
    [Test] public void SuccessBuy()
    {
        var f = ShopFeedbackFormat.Format(ShopTxResult.Success, "Wood", 10, "Coin", 50);
        Assert.IsTrue(f.Success);
        Assert.AreEqual("+10 Wood / -50 Coin", f.Message);
    }

    [Test] public void SuccessSell()
    {
        var f = ShopFeedbackFormat.Format(ShopTxResult.Success, "Coin", 50, "Wood", 10);
        Assert.AreEqual("+50 Coin / -10 Wood", f.Message);
    }

    [Test] public void FailedAffordUsesSpentName()
    {
        var f = ShopFeedbackFormat.Format(ShopTxResult.FailedAfford, "", 0, "Coin", 0);
        Assert.IsFalse(f.Success);
        Assert.AreEqual("Not enough Coin", f.Message);
    }

    [Test] public void FailedNoResourceUsesSpentName()
    {
        var f = ShopFeedbackFormat.Format(ShopTxResult.FailedNoResource, "", 0, "Wood", 0);
        Assert.AreEqual("Not enough Wood", f.Message);
    }

    [Test] public void FailedStock() =>
        Assert.AreEqual("Sold out",
            ShopFeedbackFormat.Format(ShopTxResult.FailedStock, "", 0, "", 0).Message);

    [Test] public void FailedNotSellable() =>
        Assert.AreEqual("Cannot sell this item",
            ShopFeedbackFormat.Format(ShopTxResult.FailedNotSellable, "", 0, "", 0).Message);
}
