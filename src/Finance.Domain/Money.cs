namespace Finance.Domain;

/// <summary>
/// A signed money amount in agorot (₪1 = 100 agorot) — the integer minor unit the
/// whole system stores so that <c>GROUP BY SUM</c> over a time series stays exact
/// (schema.sql sign-convention note / flagged decision 1). Negative = outflow.
/// Decimal shekels appear only at the API / UI boundary, via <see cref="Shekels"/>
/// and <see cref="FromShekels"/>.
/// </summary>
public readonly record struct Money(long Agorot) : IComparable<Money>
{
    public static readonly Money Zero = new(0);

    public static Money FromShekels(decimal shekels) =>
        new((long)decimal.Round(shekels * 100m, MidpointRounding.AwayFromZero));

    public decimal Shekels => Agorot / 100m;

    public bool IsInflow => Agorot > 0;
    public bool IsOutflow => Agorot < 0;

    public static Money operator +(Money a, Money b) => new(a.Agorot + b.Agorot);
    public static Money operator -(Money a, Money b) => new(a.Agorot - b.Agorot);
    public static Money operator -(Money value) => new(-value.Agorot);

    public int CompareTo(Money other) => Agorot.CompareTo(other.Agorot);
    public static bool operator <(Money a, Money b) => a.Agorot < b.Agorot;
    public static bool operator >(Money a, Money b) => a.Agorot > b.Agorot;
    public static bool operator <=(Money a, Money b) => a.Agorot <= b.Agorot;
    public static bool operator >=(Money a, Money b) => a.Agorot >= b.Agorot;
}
