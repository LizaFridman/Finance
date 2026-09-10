using System;
using System.Collections.Generic;
using System.Linq;
using Finance.Domain.Reporting;

namespace Finance.Tests.Domain;

/// <summary>
/// Pure math, no database. The SQL row-reader and its filters are covered
/// separately by <c>TimeSeriesReportingTests</c>.
/// </summary>
public class TimeSeriesCalculatorTests
{
    private static readonly TimeSeriesCalculator Calc = new();
    private static readonly ReportingCalendar January = new(1);

    private static DateOnly D(string s) => DateOnly.Parse(s);

    private static TransactionRow Row(string date, decimal shekels, string? bucket = null, string? category = null) =>
        new(D(date), (long)(shekels * 100m), bucket, category);

    [Fact]
    public void Monthly_series_is_ordered_and_gap_free()
    {
        var rows = new List<TransactionRow>
        {
            Row("2025-01-15", -100m),
            Row("2025-04-10", -200m), // Feb + Mar have nothing
        };

        var points = Calc.Series(rows, Grain.Month, D("2025-01-01"), D("2025-04-30"), January);

        Assert.Equal(
            new[] { D("2025-01-01"), D("2025-02-01"), D("2025-03-01"), D("2025-04-01") },
            points.Select(p => p.PeriodStart));
        Assert.Equal(0m, points[1].Measures.NetBalance); // gap month is a real zero row
    }

    [Fact]
    public void Income_expense_and_net_come_from_the_signed_amount()
    {
        var rows = new List<TransactionRow>
        {
            Row("2025-03-01", 8000m),
            Row("2025-03-02", -4200m),
            Row("2025-03-03", -300m),
        };

        var m = Calc.Series(rows, Grain.Month, D("2025-03-01"), D("2025-03-31"), January).Single().Measures;

        Assert.Equal(8000m, m.Income);
        Assert.Equal(4500m, m.Expense);    // outflows as a positive number
        Assert.Equal(3500m, m.NetBalance); // income - expense
    }

    [Fact]
    public void Cumulative_within_year_resets_at_a_non_January_year_start()
    {
        var april = new ReportingCalendar(4);
        var rows = new List<TransactionRow>
        {
            Row("2025-04-10", -100m), // RY2025 month 1
            Row("2025-05-10", -100m), // RY2025 month 2
            Row("2026-03-10", -100m), // RY2025 month 12
            Row("2026-04-10", -100m), // RY2026 month 1 -> resets
        };

        var pts = Calc.CumulativeWithinYear(rows, D("2025-04-01"), D("2026-04-30"), april)
            .ToDictionary(p => p.PeriodStart, p => p.Measures.Expense);

        Assert.Equal(100m, pts[D("2025-04-01")]);
        Assert.Equal(200m, pts[D("2025-05-01")]);
        Assert.Equal(300m, pts[D("2026-03-01")]); // still accumulating within RY2025
        Assert.Equal(100m, pts[D("2026-04-01")]); // new reporting year -> back to 100
    }

    [Fact]
    public void Year_over_year_aligns_the_same_calendar_month_across_years()
    {
        var years = new[] { 2025, 2026 };
        var (from, to) = TimeSeriesCalculator.YearOverYearRange(years, January);
        var rows = new List<TransactionRow>
        {
            Row("2025-02-15", -500m),
            Row("2026-02-15", -650m),
        };
        Assert.Equal(D("2025-01-01"), from);
        Assert.Equal(D("2026-12-31"), to);

        var yoy = Calc.YearOverYear(rows, years, January);

        var february = yoy.Rows.Single(r => r.CalendarMonth == 2);
        Assert.Equal(500m, february.ByYear[2025].Expense);
        Assert.Equal(650m, february.ByYear[2026].Expense);
        Assert.Equal(500m, yoy.YearTotals[2025].Expense);
        Assert.Equal(650m, yoy.YearTotals[2026].Expense);
    }

    [Fact]
    public void Rolling_12_month_sums_exactly_the_trailing_twelve_months()
    {
        var from = D("2025-12-01");
        var windowStart = TimeSeriesCalculator.Rolling12WindowStart(from);
        Assert.Equal(D("2025-01-01"), windowStart);

        var rows = new List<TransactionRow>
        {
            Row("2024-12-01", -100m), // 13 months before Dec-2025 -> excluded
            Row("2025-01-01", -10m),
            Row("2025-12-01", -5m),
        };

        var dec = Calc.Rolling12Month(rows, from, D("2025-12-31"), January).Single().Measures;

        Assert.Equal(15m, dec.Expense); // 10 + 5, the Dec-2024 charge has rolled off
    }

    [Fact]
    public void By_bucket_breakdown_puts_null_bucket_rows_under_needs_bucket_assignment()
    {
        var rows = new List<TransactionRow>
        {
            Row("2025-05-01", -100m, bucket: "shared"),
            Row("2025-05-02", -400m, bucket: null),
        };

        var series = Calc.SeriesByBucket(rows, Grain.Month, D("2025-05-01"), D("2025-05-31"), January);

        var pending = series.Single(s => s.Key == ReportingKeys.NeedsBucketAssignment);
        Assert.Equal(400m, pending.Points.Single().Measures.Expense);
        Assert.Contains(series, s => s.Key == "shared");
    }

    [Fact]
    public void Yearly_grain_rolls_months_into_reporting_years()
    {
        var april = new ReportingCalendar(4);
        var rows = new List<TransactionRow>
        {
            Row("2025-05-10", -100m), // RY2025
            Row("2026-01-10", -200m), // RY2025
            Row("2026-06-10", -400m), // RY2026
        };

        var points = Calc.Series(rows, Grain.Year, D("2025-04-01"), D("2026-06-30"), april);

        Assert.Equal(new[] { D("2025-04-01"), D("2026-04-01") }, points.Select(p => p.PeriodStart));
        Assert.Equal(300m, points[0].Measures.Expense);
        Assert.Equal(400m, points[1].Measures.Expense);
    }
}
