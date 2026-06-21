using NUnit.Framework;

public class ShopMathTests
{
    [Test] public void AffordLimitedByStock() =>
        Assert.AreEqual(5, ShopMath.MaxBuyQty(stockMax: 5, owned: 100, price: 10));

    [Test] public void AffordLimitedByCurrency() =>
        Assert.AreEqual(9, ShopMath.MaxBuyQty(stockMax: 10, owned: 95, price: 10));

    [Test] public void ExactlyAffordable() =>
        Assert.AreEqual(10, ShopMath.MaxBuyQty(stockMax: 10, owned: 100, price: 10));

    [Test] public void CannotAffordOne() =>
        Assert.AreEqual(0, ShopMath.MaxBuyQty(stockMax: 10, owned: 5, price: 10));

    [Test] public void FreeItemLimitedByStock() =>
        Assert.AreEqual(10, ShopMath.MaxBuyQty(stockMax: 10, owned: 0, price: 0));

    [Test] public void NegativeStockClampedToZero() =>
        Assert.AreEqual(0, ShopMath.MaxBuyQty(stockMax: -5, owned: 100, price: 10));

    [Test] public void NegativeOwnedClampedToZero() =>
        Assert.AreEqual(0, ShopMath.MaxBuyQty(stockMax: 10, owned: -1, price: 10));
}
