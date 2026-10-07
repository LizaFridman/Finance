using Finance.Domain.Reporting;

namespace Finance.Application.Reporting;

/// <summary>
/// The time-series read surface the host exposes (spec §4.2). Each call composes
/// three pieces: the live year-start (<see cref="IReportingConfig"/>), the rows in
/// range (<see cref="ITransactionRowReader"/>), and the shape math
/// (<see cref="TimeSeriesCalculator"/>). Nothing is cached.
/// </summary>
public interface ITimeSeriesReporting
{
    IReadOnlyList<SeriesPoint> Series(Grain grain, DateOnly from, DateOnly to, SeriesFilter? filter = null);
    IReadOnlyList<SeriesPoint> MonthlyTrend(DateOnly from, DateOnly to, SeriesFilter? filter = null);
    IReadOnlyList<SeriesPoint> CumulativeWithinYear(DateOnly from, DateOnly to, SeriesFilter? filter = null);
    IReadOnlyList<SeriesPoint> Rolling12Month(DateOnly from, DateOnly to, SeriesFilter? filter = null);
    YearOverYear YearOverYear(IReadOnlyList<int> years, SeriesFilter? filter = null);
    IReadOnlyList<LabeledSeries> SeriesByBucket(Grain grain, DateOnly from, DateOnly to, SeriesFilter? filter = null);
    IReadOnlyList<LabeledSeries> SeriesByCategory(Grain grain, DateOnly from, DateOnly to, SeriesFilter? filter = null);
}

/// <inheritdoc />
public sealed class TimeSeriesReporting : ITimeSeriesReporting
{
    private readonly ITransactionRowReader _rows;
    private readonly IReportingConfig _config;
    private readonly TimeSeriesCalculator _calculator;

    public TimeSeriesReporting(
        ITransactionRowReader rows, IReportingConfig config, TimeSeriesCalculator calculator)
    {
        _rows = rows;
        _config = config;
        _calculator = calculator;
    }

    private ReportingCalendar Calendar() => new(_config.GetYearStartMonth());

    public IReadOnlyList<SeriesPoint> Series(
        Grain grain, DateOnly from, DateOnly to, SeriesFilter? filter = null)
    {
        var calendar = Calendar();
        var rows = _rows.Read(from, to, filter ?? SeriesFilter.None);
        return _calculator.Series(rows, grain, from, to, calendar);
    }

    public IReadOnlyList<SeriesPoint> MonthlyTrend(DateOnly from, DateOnly to, SeriesFilter? filter = null) =>
        Series(Grain.Month, from, to, filter);

    public IReadOnlyList<SeriesPoint> CumulativeWithinYear(
        DateOnly from, DateOnly to, SeriesFilter? filter = null)
    {
        var calendar = Calendar();
        var rows = _rows.Read(from, to, filter ?? SeriesFilter.None);
        return _calculator.CumulativeWithinYear(rows, from, to, calendar);
    }

    public IReadOnlyList<SeriesPoint> Rolling12Month(
        DateOnly from, DateOnly to, SeriesFilter? filter = null)
    {
        var calendar = Calendar();
        var rows = _rows.Read(
            TimeSeriesCalculator.Rolling12WindowStart(from), to, filter ?? SeriesFilter.None);
        return _calculator.Rolling12Month(rows, from, to, calendar);
    }

    public YearOverYear YearOverYear(IReadOnlyList<int> years, SeriesFilter? filter = null)
    {
        if (years.Count == 0)
            throw new ArgumentException("At least one reporting year is required.", nameof(years));

        var calendar = Calendar();
        var (from, to) = TimeSeriesCalculator.YearOverYearRange(years, calendar);
        var rows = _rows.Read(from, to, filter ?? SeriesFilter.None);
        return _calculator.YearOverYear(rows, years, calendar);
    }

    public IReadOnlyList<LabeledSeries> SeriesByBucket(
        Grain grain, DateOnly from, DateOnly to, SeriesFilter? filter = null)
    {
        var calendar = Calendar();
        var rows = _rows.Read(from, to, filter ?? SeriesFilter.None);
        return _calculator.SeriesByBucket(rows, grain, from, to, calendar);
    }

    public IReadOnlyList<LabeledSeries> SeriesByCategory(
        Grain grain, DateOnly from, DateOnly to, SeriesFilter? filter = null)
    {
        var calendar = Calendar();
        var rows = _rows.Read(from, to, filter ?? SeriesFilter.None);
        return _calculator.SeriesByCategory(rows, grain, from, to, calendar);
    }
}
