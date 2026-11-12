namespace EventsWorld.Shared.Kernel.Domain;

/// <summary>
/// An amount with its ISO 4217 currency, stored as <c>numeric(12,2)</c> + <c>char(3)</c> (NFR-05.2).
/// Amounts with more than two decimals are rejected instead of rounded, so a calculation error
/// never goes unnoticed. Negative amounts are allowed: ledger entries are signed.
/// </summary>
public sealed record Money
{
    public decimal Amount { get; }
    public string Currency { get; }

    public Money(decimal amount, string currency)
    {
        if (string.IsNullOrWhiteSpace(currency)
            || currency.Length != 3
            || !currency.All(char.IsAsciiLetter))
        {
            throw new ArgumentException("Currency must be a 3-letter ISO 4217 code.", nameof(currency));
        }

        if (decimal.Round(amount, 2) != amount)
        {
            throw new ArgumentException("Amount cannot have more than two decimals.", nameof(amount));
        }

        Amount = amount;
        Currency = currency.ToUpperInvariant();
    }

    public static Money Zero(string currency) => new(0m, currency);

    public static Money operator +(Money left, Money right)
    {
        EnsureSameCurrency(left, right);
        return new Money(left.Amount + right.Amount, left.Currency);
    }

    public static Money operator -(Money left, Money right)
    {
        EnsureSameCurrency(left, right);
        return new Money(left.Amount - right.Amount, left.Currency);
    }

    public override string ToString() => $"{Amount:0.00} {Currency}";

    private static void EnsureSameCurrency(Money left, Money right)
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);

        if (left.Currency != right.Currency)
        {
            throw new InvalidOperationException(
                $"Cannot combine money in '{left.Currency}' and '{right.Currency}'.");
        }
    }
}
