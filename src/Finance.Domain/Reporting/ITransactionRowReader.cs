namespace Finance.Domain.Reporting;

/// <summary>
/// A transaction reduced to what time-series reporting needs: when, how much
/// (signed agorot — negative is an outflow), and the two classification keys.
/// Produced by <see cref="ITransactionRowReader"/>, consumed by
/// <see cref="TimeSeriesCalculator"/>.
/// </summary>
public readonly record struct TransactionRow(
    DateOnly Date, Money Amount, string? BucketId, string? CategoryId);

/// <summary>
/// The single persistence touch-point for reporting: returns the raw rows a
/// report needs, already narrowed by <paramref name="filter"/> and the date
/// range. All period bucketing and arithmetic happens afterwards in
/// <see cref="TimeSeriesCalculator"/>, over the rows this returns.
/// </summary>
public interface ITransactionRowReader
{
    IReadOnlyList<TransactionRow> Read(DateOnly fromInclusive, DateOnly toInclusive, SeriesFilter filter);
}
