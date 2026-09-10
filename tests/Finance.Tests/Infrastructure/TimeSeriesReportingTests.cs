using System;
using System.Linq;
using Finance.Domain.Reporting;
using Finance.Infrastructure;
using Finance.Application.Reporting;
using Finance.Infrastructure.Reporting;

namespace Finance.Tests.Infrastructure;

public class TimeSeriesReportingTests
{
    private static ITimeSeriesReporting Reporting(TempDatabase t) =>
        new TimeSeriesReporting(new TransactionRowReader(t.Db), new ConfigStore(t.Db), new TimeSeriesCalculator());

    private static DateOnly D(string s) => DateOnly.Parse(s);

    [Fact]
    public void Monthly_series_is_ordered_and_gap_free()
    {
        using var t = new TempDatabase();
        TestData.InsertTx(t.Db, "a", "2025-01-15", -100m);
        TestData.InsertTx(t.Db, "b", "2025-04-10", -200m); // Feb + Mar have nothing

        var points = Reporting(t).MonthlyTrend(D("2025-01-01"), D("2025-04-30"));

        Assert.Equal(
            new[] { D("2025-01-01"), D("2025-02-01"), D("2025-03-01"), D("2025-04-01") },
            points.Select(p => p.PeriodStart));
        Assert.Equal(0m, points[1].Measures.NetBalance); // gap month is a real zero row
    }

    [Fact]
    public void Income_expense_and_net_come_from_the_signed_amount()
    {
        using var t = new TempDatabase();
        TestData.InsertTx(t.Db, "salary", "2025-03-01", 8000m);
        TestData.InsertTx(t.Db, "rent", "2025-03-02", -4200m);
        TestData.InsertTx(t.Db, "shop", "2025-03-03", -300m);

        var m = Reporting(t).MonthlyTrend(D("2025-03-01"), D("2025-03-31")).Single().Measures;

        Assert.Equal(8000m, m.Income);
        Assert.Equal(4500m, m.Expense);         // outflows as a positive number
        Assert.Equal(3500m, m.NetBalance);      // income - expense
    }

    [Fact]
    public void Cumulative_within_year_resets_at_a_non_January_year_start()
    {
        using var t = new TempDatabase();
        new ConfigStore(t.Db).SetYearStartMonth(4); // reporting year starts in April

        TestData.InsertTx(t.Db, "1", "2025-04-10", -100m); // RY2025 month 1
        TestData.InsertTx(t.Db, "2", "2025-05-10", -100m); // RY2025 month 2
        TestData.InsertTx(t.Db, "3", "2026-03-10", -100m); // RY2025 month 12
        TestData.InsertTx(t.Db, "4", "2026-04-10", -100m); // RY2026 month 1 -> resets

        var pts = Reporting(t).CumulativeWithinYear(D("2025-04-01"), D("2026-04-30"))
            .ToDictionary(p => p.PeriodStart, p => p.Measures.Expense);

        Assert.Equal(100m, pts[D("2025-04-01")]);
        Assert.Equal(200m, pts[D("2025-05-01")]);
        Assert.Equal(300m, pts[D("2026-03-01")]); // still accumulating within RY2025
        Assert.Equal(100m, pts[D("2026-04-01")]); // new reporting year -> back to 100
    }

    [Fact]
    public void Year_over_year_aligns_the_same_calendar_month_across_years()
    {
        using var t = new TempDatabase();
        TestData.InsertTx(t.Db, "f25", "2025-02-15", -500m);
        TestData.InsertTx(t.Db, "f26", "2026-02-15", -650m);

        var yoy = Reporting(t).YearOverYear(new[] { 2025, 2026 });

        var february = yoy.Rows.Single(r => r.CalendarMonth == 2);
        Assert.Equal(500m, february.ByYear[2025].Expense);
        Assert.Equal(650m, february.ByYear[2026].Expense);
        Assert.Equal(500m, yoy.YearTotals[2025].Expense);
        Assert.Equal(650m, yoy.YearTotals[2026].Expense);
    }

    [Fact]
    public void Rolling_12_month_sums_exactly_the_trailing_twelve_months()
    {
        using var t = new TempDatabase();
        TestData.InsertTx(t.Db, "old", "2024-12-01", -100m); // 13 months before Dec-2025 -> excluded
        TestData.InsertTx(t.Db, "in1", "2025-01-01", -10m);  // within the Jan..Dec 2025 window
        TestData.InsertTx(t.Db, "in2", "2025-12-01", -5m);

        var dec = Reporting(t).Rolling12Month(D("2025-12-01"), D("2025-12-31")).Single().Measures;

        Assert.Equal(15m, dec.Expense); // 10 + 5, the Dec-2024 charge has rolled off
    }

    [Fact]
    public void Null_bucket_rows_are_excluded_when_asked()
    {
        using var t = new TempDatabase();
        TestData.InsertTx(t.Db, "assigned", "2025-05-01", -100m, bucket: "shared");
        TestData.InsertTx(t.Db, "pending", "2025-05-02", -400m, bucket: null);

        var included = Reporting(t).MonthlyTrend(D("2025-05-01"), D("2025-05-31")).Single().Measures;
        var excluded = Reporting(t).MonthlyTrend(
            D("2025-05-01"), D("2025-05-31"),
            new SeriesFilter { NullBuckets = NullBucketHandling.Exclude }).Single().Measures;

        Assert.Equal(500m, included.Expense);
        Assert.Equal(100m, excluded.Expense);
    }

    [Fact]
    public void By_bucket_breakdown_surfaces_a_needs_bucket_assignment_series()
    {
        using var t = new TempDatabase();
        TestData.InsertTx(t.Db, "s", "2025-05-01", -100m, bucket: "shared");
        TestData.InsertTx(t.Db, "p", "2025-05-02", -400m, bucket: null);

        var series = Reporting(t).SeriesByBucket(Grain.Month, D("2025-05-01"), D("2025-05-31"));

        var pending = series.Single(s => s.Key == ReportingKeys.NeedsBucketAssignment);
        Assert.Equal(400m, pending.Points.Single().Measures.Expense);
        Assert.Contains(series, s => s.Key == "shared");
    }

    [Fact]
    public void Reclassifying_one_transaction_moves_every_dependent_view()
    {
        using var t = new TempDatabase();
        // Two months, one row each, both initially in 'shared'.
        TestData.InsertTx(t.Db, "x", "2025-01-10", -100m, bucket: "shared");
        TestData.InsertTx(t.Db, "y", "2025-02-10", -300m, bucket: "shared");
        var q = Reporting(t);
        var from = D("2025-01-01");
        var to = D("2025-02-28");
        var sharedOnly = new SeriesFilter { BucketId = "shared" };
        var lizaOnly = new SeriesFilter { BucketId = "personal_liza" };

        decimal SharedTrend() => q.MonthlyTrend(from, to, sharedOnly).Sum(p => p.Measures.Expense);
        decimal LizaTrend() => q.MonthlyTrend(from, to, lizaOnly).Sum(p => p.Measures.Expense);
        decimal SharedCumulative() => q.CumulativeWithinYear(from, to, sharedOnly).Last().Measures.Expense;
        decimal SharedRolling() => q.Rolling12Month(from, to, sharedOnly).Last().Measures.Expense;
        decimal SharedByBucket() => q.SeriesByBucket(Grain.Month, from, to)
            .Single(s => s.Key == "shared").Points.Sum(p => p.Measures.Expense);

        Assert.Equal(400m, SharedTrend());
        Assert.Equal(0m, LizaTrend());
        Assert.Equal(400m, SharedCumulative());
        Assert.Equal(400m, SharedRolling());
        Assert.Equal(400m, SharedByBucket());

        // Move the February row from 'shared' to 'personal_liza' — one UPDATE.
        TestData.MoveBucket(t.Db, "y", "personal_liza");

        Assert.Equal(100m, SharedTrend());        // every view recomputed from current state
        Assert.Equal(300m, LizaTrend());
        Assert.Equal(100m, SharedCumulative());
        Assert.Equal(100m, SharedRolling());
        Assert.Equal(100m, SharedByBucket());
    }
}
