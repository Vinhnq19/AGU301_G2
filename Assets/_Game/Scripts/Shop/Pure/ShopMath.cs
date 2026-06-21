using System;

/// <summary>Pure shop math. No Unity dependency — unit-testable.</summary>
public static class ShopMath
{
    /// <summary>
    /// Max units a player can buy now, clamped by BOTH remaining stock and what the
    /// player can afford. Floored at 0.
    /// </summary>
    /// <param name="stockMax">Stock cap. For unlimited items, pass the caller's safety cap.</param>
    /// <param name="owned">Currency the player holds (in the item's CurrencyType).</param>
    /// <param name="price">Per-unit price. &lt;= 0 means free (affordability capped only by stockMax).</param>
    public static int MaxBuyQty(int stockMax, int owned, int price)
    {
        if (stockMax < 0) stockMax = 0;
        if (owned < 0) owned = 0;
        int affordable = price > 0 ? owned / price : int.MaxValue;
        return Math.Min(affordable, stockMax);
    }
}
