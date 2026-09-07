using System.Text;
using Finance.Core.Reporting;

namespace Finance.Data.Queries;

/// <summary>
/// Time-progression-first reporting (spec §4.2, plan step 6). Every result is an
/// ordered, gap-free series of periods carrying income / expense / net balance.
/// Nothing is cached: each call issues a live <c>SELECT</c> over the current
/// table state. The period bucketing, the configurable reporting-year start, the
/// cumulative reset, the trailing-12 window and year-over-year alignment are all
/// done in C# — they are far clearer here than as <c>strftime</c> SQL, and the
/// read itself stays a plain live query.
/// </summary>
public sealed class TimeSeriesQueries
{
    private readonly SqliteDatabase _db;
    private readonly ConfigStore _config;

    public TimeSeriesQueries(SqliteDatabase db, ConfigStore config)
    {
        _db = db;
        _config = config;
    }

    private ReportingCalendar Calendar() => new(_config.GetYearStartMonth());

    // --- public surface --------------------------------------------------------

    public IReadOnlyList<SeriesPoint> Series(Grain grain, DateOnly from, DateOnly to, SeriesFilter? filter = null)
    {
        var cal = Calendar();
        var byPeriod = Fetch(from, to, filter ?? SeriesFilter.None)
            .GroupBy(r => PeriodStart(r.Date, grain, cal))
            .ToDictionary(g => g.Key, Aggregate);

        return Periods(from, to, grain, cal)
            .Select(p => new SeriesPoint(p, byPeriod.GetValueOrDefault(p, Measures.Zero)))
            .ToList();
    }

    public IReadOnlyList<SeriesPoint> MonthlyTrend(DateOnly from, DateOnly to, SeriesFilter? filter = null) =>
        Series(Grain.Month, from, to, filter);

    public IReadOnlyList<SeriesPoint> CumulativeWithinYear(DateOnly from, DateOnly to, SeriesFilter? filter = null)
    {
        var cal = Calendar();
        var result = new List<SeriesPoint>();
        int? currentYear = null;
        var running = Measures.Zero;

        foreach (var point in Series(Grain.Month, from, to, filter))
        {
            var label = cal.YearLabelOf(point.PeriodStart);
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

    public IReadOnlyList<SeriesPoint> Rolling12Month(DateOnly from, DateOnly to, SeriesFilter? filter = null)
    {
        var firstMonth = new DateOnly(from.Year, from.Month, 1);
        var windowStart = firstMonth.AddMonths(-11);

        var monthly = Fetch(windowStart, to, filter ?? SeriesFilter.None)
            .GroupBy(r => new DateOnly(r.Date.Year, r.Date.Month, 1))
            .ToDictionary(g => g.Key, Aggregate);

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

    public YearOverYear YearOverYear(IReadOnlyList<int> years, SeriesFilter? filter = null)
    {
        if (years.Count == 0)
            throw new ArgumentException("At least one reporting year is required.", nameof(years));

        var cal = Calendar();
        var from = cal.StartOf(years.Min());
        var to = cal.EndExclusiveOf(years.Max()).AddDays(-1);

        var cells = new Dictionary<(int Ordinal, int Year), List<Row>>();
        foreach (var row in Fetch(from, to, filter ?? SeriesFilter.None))
        {
            var label = cal.YearLabelOf(row.Date);
            if (!years.Contains(label))
                continue;
            var key = (cal.OrdinalMonthOf(row.Date), label);
            (cells.TryGetValue(key, out var list) ? list : cells[key] = new()).Add(row);
        }

        var rows = new List<YearOverYearRow>(12);
        for (var ordinal = 1; ordinal <= 12; ordinal++)
        {
            var byYear = years.ToDictionary(
                y => y,
                y => cells.TryGetValue((ordinal, y), out var list) ? Aggregate(list) : Measures.Zero);
            rows.Add(new YearOverYearRow(ordinal, cal.CalendarMonthAt(ordinal), byYear));
        }

        var totals = years.ToDictionary(
            y => y,
            y => rows.Aggregate(Measures.Zero, (acc, r) => acc.Plus(r.ByYear[y])));

        return new YearOverYear(cal.YearStartMonth, years, rows, totals);
    }

    public IReadOnlyList<LabeledSeries> SeriesByBucket(
        Grain grain, DateOnly from, DateOnly to, SeriesFilter? filter = null) =>
        Breakdown(grain, from, to, filter, r => r.Bucket ?? ReportingKeys.NeedsBucketAssignment);

    public IReadOnlyList<LabeledSeries> SeriesByCategory(
        Grain grain, DateOnly from, DateOnly to, SeriesFilter? filter = null) =>
        Breakdown(grain, from, to, filter, r => r.Category ?? ReportingKeys.Uncategorized);

    // --- internals -----------------------------------------------------------

    private readonly record struct Row(DateOnly Date, long Amount, string? Bucket, string? Category);

    private IReadOnlyList<LabeledSeries> Breakdown(
        Grain grain, DateOnly from, DateOnly to, SeriesFilter? filter, Func<Row, string> keyOf)
    {
        var cal = Calendar();
        var periods = Periods(from, to, grain, cal).ToList();

        return Fetch(from, to, filter ?? SeriesFilter.None)
            .GroupBy(keyOf)
            .OrderBy(g => g.Key, StringComparer.Ordinal)
            .Select(group =>
            {
                var byPeriod = group
                    .GroupBy(r => PeriodStart(r.Date, grain, cal))
                    .ToDictionary(g => g.Key, Aggregate);
                var points = periods
                    .Select(p => new SeriesPoint(p, byPeriod.GetValueOrDefault(p, Measures.Zero)))
                    .ToList();
                return new LabeledSeries(group.Key, points);
            })
            .ToList();
    }

    private List<Row> Fetch(DateOnly from, DateOnly to, SeriesFilter filter)
    {
        using var connection = _db.OpenConnection();
        using var cmd = connection.CreateCommand();

        var sql = new StringBuilder(
            "SELECT date, amount, bucket_id, category_id FROM transactions " +
            "WHERE date >= $from AND date <= $to");
        cmd.Parameters.AddWithValue("$from", from.ToString("yyyy-MM-dd"));
        cmd.Parameters.AddWithValue("$to", to.ToString("yyyy-MM-dd"));

        if (filter.SourceId is { } source)
        {
            sql.Append(" AND source_id = $source");
            cmd.Parameters.AddWithValue("$source", source);
        }
        if (filter.CategoryId is { } category)
        {
            sql.Append(" AND category_id = $category");
            cmd.Parameters.AddWithValue("$category", category);
        }
        if (filter.BucketId is { } bucket)
        {
            sql.Append(" AND bucket_id = $bucket");
            cmd.Parameters.AddWithValue("$bucket", bucket);
        }
        else
        {
            // bucket_id IS NULL is an explicit decision, never an implicit one (spec §4.2).
            if (filter.NullBuckets == NullBucketHandling.Exclude)
                sql.Append(" AND bucket_id IS NOT NULL");
            if (filter.HidePersonalAkumu)
                sql.Append(" AND (bucket_id IS NULL OR bucket_id <> 'personal_akumu')");
        }

        cmd.CommandText = sql.ToString();
        using var reader = cmd.ExecuteReader();
        var rows = new List<Row>();
        while (reader.Read())
        {
            rows.Add(new Row(
                DateOnly.ParseExact(reader.GetString(0), "yyyy-MM-dd"),
                reader.GetInt64(1),
                reader.IsDBNull(2) ? null : reader.GetString(2),
                reader.IsDBNull(3) ? null : reader.GetString(3)));
        }
        return rows;
    }

    private static Measures Aggregate(IEnumerable<Row> rows)
    {
        long income = 0, expense = 0;
        foreach (var row in rows)
        {
            if (row.Amount > 0)
                income += row.Amount;
            else
                expense += -row.Amount;
        }
        return Measures.FromAgorot(income, expense);
    }

    private static DateOnly PeriodStart(DateOnly date, Grain grain, ReportingCalendar cal) => grain switch
    {
        Grain.Day => date,
        Grain.Month => new DateOnly(date.Year, date.Month, 1),
        Grain.Year => cal.StartOf(cal.YearLabelOf(date)),
        _ => throw new ArgumentOutOfRangeException(nameof(grain)),
    };

    private static IEnumerable<DateOnly> Periods(DateOnly from, DateOnly to, Grain grain, ReportingCalendar cal)
    {
        switch (grain)
        {
            case Grain.Day:
                for (var d = from; d <= to; d = d.AddDays(1))
                    yield return d;
                break;
            case Grain.Month:
                for (var d = new DateOnly(from.Year, from.Month, 1); d <= to; d = d.AddMonths(1))
                    yield return d;
                break;
            case Grain.Year:
                for (var y = cal.YearLabelOf(from); y <= cal.YearLabelOf(to); y++)
                    yield return cal.StartOf(y);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(grain));
        }
    }
}
