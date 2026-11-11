using EventsWorld.Shared.Kernel.Domain;

namespace EventsWorld.UnitTests;

public sealed class MoneyTests
{
    [Fact]
    public void Constructor_ShouldRoundToTwoDecimals_AndNormalizeCurrency()
    {
        var money = new Money(12.345m, "usd");

        Assert.Equal(12.35m, money.Amount);
        Assert.Equal("USD", money.Currency);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("US")]
    [InlineData("USDD")]
    public void Constructor_WithInvalidCurrency_ShouldThrowArgumentException(string? invalidCurrency)
    {
        Assert.Throws<ArgumentException>(() => new Money(10m, invalidCurrency!));
    }

    [Fact]
    public void Addition_WithMatchingCurrency_ShouldSumAmounts()
    {
        var total = new Money(10.50m, "EUR") + new Money(4.25m, "eur");

        Assert.Equal(14.75m, total.Amount);
        Assert.Equal("EUR", total.Currency);
    }

    [Fact]
    public void Addition_WithDifferentCurrencies_ShouldThrowInvalidOperationException()
    {
        var usd = new Money(10m, "USD");
        var eur = new Money(10m, "EUR");

        Assert.Throws<InvalidOperationException>(() => _ = usd + eur);
    }
}
