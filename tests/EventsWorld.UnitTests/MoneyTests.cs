using EventsWorld.Shared.Kernel.Domain;

namespace EventsWorld.UnitTests;

public sealed class MoneyTests
{
    [Fact]
    public void Constructor_ShouldKeepTheAmount_AndNormalizeCurrency()
    {
        var money = new Money(120.50m, "bob");

        Assert.Equal(120.50m, money.Amount);
        Assert.Equal("BOB", money.Currency);
    }

    [Fact]
    public void Constructor_WithMoreThanTwoDecimals_ShouldThrowInsteadOfRounding()
    {
        Assert.Throws<ArgumentException>(() => new Money(12.345m, "BOB"));
    }

    [Fact]
    public void Zero_ShouldUseTheGivenCurrency()
    {
        var zero = Money.Zero("BOB");

        Assert.Equal(0m, zero.Amount);
        Assert.Equal("BOB", zero.Currency);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("US")]
    [InlineData("USDD")]
    [InlineData("B1$")]
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
