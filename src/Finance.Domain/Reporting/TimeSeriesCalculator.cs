namespace Finance.Domain.Reporting;

/// <summary>
/// Time-progression-first reporting math (spec §4.2). Pure: every method is a
/// function of the rows it is handed plus a <see cref="ReportingCalendar"/> — no
/// database, no clock, no configuration lookup. The caller (an application
/// service) is responsible for reading rows that cover the range each method
/// needs; <see cref="Rolling12WindowStart"/> and <see cref="YearOverYearRange"/>
/// state those ranges.
///
/// Every result is an ordered, gap-free series of periods carrying
/// income / expense / net balance.
/// </summary>
public sealed class TimeSeriesCalculator
{
    /// <summary>
    /// Income / expense / net for one grain over <paramref name="from"/>..<paramref name="to"/>.
    /// <paramref name="rows"/> must cover that range.
    /// </summary>
    public IReadOnlyList<SeriesPoint> Series(
        IReadOnlyList<TransactionRow> rows, Grain grain, DateOnly from, DateOnly to, ReportingCalendar calendar)
    {
        var byPeriod = BucketByPeriod(rows, grain, calendar);
        return FillGaps(Periods(from, to, grain, calendar), byPeriod);
    }

    /// <summary>
    /// Monthly series where each point is the running total since the start of
    /// its reporting year; the total resets to zero at every year boundary.
    /// </summary>
    public IReadOnlyList<SeriesPoint> CumulativeWithinYear(
        IReadOnlyList<TransactionRow> rows, DateOnly from, DateOnly to, ReportingCalendar calendar)
    {
        var result = new List<SeriesPoint>();
        int? currentYear = null;
        var running = Measures.Zero;

        foreach (var point in Series(rows, Grain.Month, from, to, calendar))
        {
            var label = calendar.YearLabelOf(point.PeriodStart);
            if (currentYear != label)
            {
                running = Measures.Zero;
                currentYear = label;
            }
            running = running.Plus(point.Measures);
            result.Add(new SeriesPoint(point.PeriodStart, running));
        }

        return result;
    }

    /// <summary>Row range a <see cref="Rolling12Month"/> call needs, given its <paramref name="from"/>.</summary>
    public static DateOnly Rolling12WindowStart(DateOnly from) =>
        new DateOnly(from.Year, from.Month, 1).AddMonths(-11);

    /// <summary>
    /// Monthly series where each point is the sum of that month and the 11
    /// before it. <paramref name="rows"/> must start at
    /// <see cref="Rolling12WindowStart"/>(<paramref name="from"/>).
    /// </summary>
    public IReadOnlyList<SeriesPoint> Rolling12Month(
        IReadOnlyList<TransactionRow> rows, DateOnly from, DateOnly to, ReportingCalendar calendar)
    {
        var firstMonth = Truncate(from, Grain.Month, calendar);
        var monthly = BucketByPeriod(rows, Grain.Month, calendar);

        var result = new List<SeriesPoint>();
        for (var month = firstMonth; month <= to; month = month.AddMonths(1))
        {
            var window = Measures.Zero;
            for (var w = month.AddMonths(-11); w <= month; w = w.AddMonths(1))
                window = window.Plus(monthly.GetValueOrDefault(w, Measures.Zero));
            result.Add(new SeriesPoint(month, window));
        }

        return result;
    }

    /// <summary>Calendar range a <see cref="YearOverYear"/> call needs, given its reporting years.</summary>
    public static (DateOnly From, DateOnly To) YearOverYearRange(
        IReadOnlyList<int> years, ReportingCalendar calendar) =>
        (calendar.StartOf(years.Min()), calendar.EndExclusiveOf(years.Max()).AddDays(-1));

    /// <summary>
    /// The reporting years laid side by side, one row per ordinal month (1 == the
    /// year-start month), so the same calendar month lines up across years.
    /// </summary>
    public YearOverYear YearOverYear(
        IReadOnlyList<TransactionRow> rows, IReadOnlyList<int> years, ReportingCalendar calendar)
    {
        if (years.Count == 0)
            throw new ArgumentException("At least one reporting year is required.", nameof(years));

        var cells = new Dictionary<(int Ordinal, int Year), List<TransactionRow>>();
        foreach (var row in rows)
        {
            var label = calendar.YearLabelOf(row.Date);
            if (!years.Contains(label))
                continue;
            var key = (calendar.OrdinalMonthOf(row.Date), label);
            (cells.TryGetValue(key, out var list) ? list : cells[key] = []).Add(row);
        }

        var yoyRows = new List<YearOverYearRow>(12);
        for (var ordinal = 1; ordinal <= 12; ordinal++)
        {
            var byYear = years.ToDictionary(
                y => y,
                y => cells.TryGetValue((ordinal, y), out var list) ? Aggregate(list) : Measures.Zero);
            yoyRows.Add(new YearOverYearRow(ordinal, calendar.CalendarMonthAt(ordinal), byYear));
        }

        var totals = years.ToDictionary(
            y => y,
            y => yoyRows.Aggregate(Measures.Zero, (acc, r) => acc.Plus(r.ByYear[y])));

        return new YearOverYear(calendar.YearStartMonth, years, yoyRows, totals);
    }

    /// <summary>Per-bucket breakdown; rows with no bucket land under <see cref="ReportingKeys.NeedsBucketAssignment"/>.</summary>
    public IReadOnlyList<LabeledSeries> SeriesByBucket(
        IReadOnlyList<TransactionRow> rows, Grain grain, DateOnly from, DateOnly to, ReportingCalendar calendar) =>
        Breakdown(rows, grain, from, to, calendar, r => r.BucketId ?? ReportingKeys.NeedsBucketAssignment);

    /// <summary>Per-category breakdown; rows with no category land under <see cref="ReportingKeys.Uncategorized"/>.</summary>
    public IReadOnlyList<LabeledSeries> SeriesByCategory(
        IReadOnlyList<TransactionRow> rows, Grain grain, DateOnly from, DateOnly to, ReportingCalendar calendar) =>
        Breakdown(rows, grain, from, to, calendar, r => r.CategoryId ?? ReportingKeys.Uncategorized);

    // --- internals -----------------------------------------------------------

    private IReadOnlyList<LabeledSeries> Breakdown(
        IReadOnlyList<TransactionRow> rows, Grain grain, DateOnly from, DateOnly to,
        ReportingCalendar calendar, Func<TransactionRow, string> keyOf)
    {
        var periods = Periods(from, to, grain, calendar).ToList();

        return rows
            .GroupBy(keyOf)
            .OrderBy(g => g.Key, StringComparer.Ordinal)
            .Select(group => new LabeledSeries(
                group.Key,
                FillGaps(periods, BucketByPeriod(group, grain, calendar))))
            .ToList();
    }

    private static Dictionary<DateOnly, Measures> BucketByPeriod(
        IEnumerable<TransactionRow> rows, Grain grain, ReportingCalendar calendar) =>
        rows.GroupBy(r => Truncate(r.Date, grain, calendar))
            .ToDictionary(g => g.Key, Aggregate);

    private static List<SeriesPoint> FillGaps(
        IEnumerable<DateOnly> periods, IReadOnlyDictionary<DateOnly, Measures> byPeriod) =>
        periods.Select(p => new SeriesPoint(p, byPeriod.GetValueOrDefault(p, Measures.Zero))).ToList();

    private static Measures Aggregate(IEnumerable<TransactionRow> rows)
    {
        var income = Money.Zero;
        var expense = Money.Zero;
        foreach (var row in rows)
        {
            if (row.Amount.IsInflow)
                income += row.Amount;
            else
                expense += -row.Amount; // outflow magnitude as a positive amount
        }
        return Measures.From(income, expense);
    }

    // The one place grain semantics live: how a date maps to its period start,
    // and how to step to the next period. Adding Week/Quarter is a change here only.
    private static DateOnly Truncate(DateOnly date, Grain grain, ReportingCalendar calendar) => grain switch
    {
        Grain.Day => date,
        Grain.Month => new DateOnly(date.Year, date.Month, 1),
        Grain.Year => calendar.StartOf(calendar.YearLabelOf(date)),
        _ => throw new ArgumentOutOfRangeException(nameof(grain)),
    };

    private static DateOnly Step(DateOnly periodStart, Grain grain) => grain switch
    {
        Grain.Day => periodStart.AddDays(1),
        Grain.Month => periodStart.AddMonths(1),
        Grain.Year => periodStart.AddYears(1),
        _ => throw new ArgumentOutOfRangeException(nameof(grain)),
    };

    private static IEnumerable<DateOnly> Periods(DateOnly from, DateOnly to, Grain grain, ReportingCalendar calendar)
    {
        for (var period = Truncate(from, grain, calendar); period <= to; period = Step(period, grain))
            yield return period;
    }
}
