namespace Finance.Domain.Reporting;

/// <summary>Time bucket size for a series. Day is the finest; Month and Year roll up from it.</summary>
public enum Grain
{
    Day,
    Month,
    Year,
}

/// <summary>
/// How a query treats transactions with no bucket yet (spec §4.2 — ingestion
/// happens before bucket assignment, so this is never an implicit choice).
/// </summary>
public enum NullBucketHandling
{
    /// <summary>Count null-bucket rows in the totals.</summary>
    Include,

    /// <summary>Leave null-bucket rows out of the totals entirely.</summary>
    Exclude,
}

/// <summary>
/// The three measures every series carries, in shekels (decimal at the boundary).
/// <c>Income</c> = sum of inflows, <c>Expense</c> = sum of outflows as a positive
/// number, <c>NetBalance</c> = Income − Expense = sum of the signed amounts.
/// </summary>
public readonly record struct Measures(decimal Income, decimal Expense, decimal NetBalance)
{
    public static readonly Measures Zero = new(0m, 0m, 0m);

    public static Measures From(Money income, Money expense) => new(
        income.Shekels,
        expense.Shekels,
        (income - expense).Shekels);

    public Measures Plus(Measures other) => new(
        Income + other.Income,
        Expense + other.Expense,
        NetBalance + other.NetBalance);
}

/// <summary>One period on the time axis and its measures.</summary>
public sealed record SeriesPoint(DateOnly PeriodStart, Measures Measures);

/// <summary>A named time series — one bucket, one category, etc.</summary>
public sealed record LabeledSeries(string Key, IReadOnlyList<SeriesPoint> Points);

/// <summary>
/// Filters applied before bucketing. All optional; the defaults select every
/// transaction with null-bucket rows included.
/// </summary>
public sealed record SeriesFilter
{
    public string? SourceId { get; init; }
    public string? CategoryId { get; init; }
    public string? BucketId { get; init; }

    /// <summary>The "hide personal data" dashboard toggle — drops <c>personal_akumu</c> (spec §4.2). No auth.</summary>
    public bool HidePersonalAkumu { get; init; }

    public NullBucketHandling NullBuckets { get; init; } = NullBucketHandling.Include;

    public static SeriesFilter None { get; } = new();
}

/// <summary>Key used for the synthetic "no bucket yet" series in a by-bucket breakdown.</summary>
public static class ReportingKeys
{
    public const string NeedsBucketAssignment = "needs_bucket_assignment";
    public const string Uncategorized = "uncategorized";
}

public sealed record YearOverYearRow(
    int OrdinalMonth,
    int CalendarMonth,
    IReadOnlyDictionary<int, Measures> ByYear);

public sealed record YearOverYear(
    int YearStartMonth,
    IReadOnlyList<int> Years,
    IReadOnlyList<YearOverYearRow> Rows,
    IReadOnlyDictionary<int, Measures> YearTotals);
