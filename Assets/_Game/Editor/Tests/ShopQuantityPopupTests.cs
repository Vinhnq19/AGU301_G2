using NUnit.Framework;

namespace DungeonBuilder.Tests
{
    public sealed class ShopQuantityPopupTests
    {
        [Test]
        public void ClampQuantity_ReturnsOneWhenRawBelowOne()
        {
            Assert.That(ShopQuantityPopup.ClampQuantity(0, 10), Is.EqualTo(1));
            Assert.That(ShopQuantityPopup.ClampQuantity(-5, 10), Is.EqualTo(1));
        }

        [Test]
        public void ClampQuantity_ClampsToMaxWhenRawExceedsMax()
        {
            Assert.That(ShopQuantityPopup.ClampQuantity(99, 10), Is.EqualTo(10));
        }

        [Test]
        public void ClampQuantity_ReturnsRawWhenInRange()
        {
            Assert.That(ShopQuantityPopup.ClampQuantity(1, 10), Is.EqualTo(1));
            Assert.That(ShopQuantityPopup.ClampQuantity(5, 10), Is.EqualTo(5));
            Assert.That(ShopQuantityPopup.ClampQuantity(10, 10), Is.EqualTo(10));
        }

        [Test]
        public void ClampQuantity_FloorsMaxAtOneSoNeverReturnsZeroOrNegative()
        {
            // max <= 0 (sold out / owns 0) → ceiling được floor về 1.
            // (Defensive — Presenter đã guard maxQty <= 0 nên không mở popup trong trường hợp này.)
            Assert.That(ShopQuantityPopup.ClampQuantity(5, 0), Is.EqualTo(1));
            Assert.That(ShopQuantityPopup.ClampQuantity(5, -3), Is.EqualTo(1));
        }

        [Test]
        public void FormatConfirmLabel_ShowsBuyLabelWithQuantityTimesPrice()
        {
            Assert.That(ShopQuantityPopup.FormatConfirmLabel(ShopAction.Buy, 5, 10), Is.EqualTo("Buy - 50$"));
        }

        [Test]
        public void FormatConfirmLabel_ShowsSellLabelWithQuantityTimesSellPrice()
        {
            Assert.That(ShopQuantityPopup.FormatConfirmLabel(ShopAction.Sell, 3, 20), Is.EqualTo("Sell - 60$"));
        }

        [Test]
        public void FormatConfirmLabel_ZeroQuantityShowsZeroTotal()
        {
            Assert.That(ShopQuantityPopup.FormatConfirmLabel(ShopAction.Buy, 0, 10), Is.EqualTo("Buy - 0$"));
        }
    }
}
