namespace Finance.Domain.Reporting;

/// <summary>
/// The configurable reporting-year calendar (spec §3). With a start month of 1
/// this is just the Gregorian calendar year; with, say, 4 the reporting year
/// "2025" runs Apr 2025 → Mar 2026. Drives yearly rollups, the
/// cumulative-within-year reset, and year-over-year alignment.
/// </summary>
public sealed class ReportingCalendar
{
    public int YearStartMonth { get; }

    public ReportingCalendar(int yearStartMonth)
    {
        if (yearStartMonth is < 1 or > 12)
            throw new ArgumentOutOfRangeException(nameof(yearStartMonth), yearStartMonth, "1–12.");
        YearStartMonth = yearStartMonth;
    }

    /// <summary>The reporting-year label a date falls in (e.g. Feb 2026 → 2025 when the year starts in April).</summary>
    public int YearLabelOf(DateOnly date) =>
        date.Month >= YearStartMonth ? date.Year : date.Year - 1;

    /// <summary>First calendar day of a reporting year.</summary>
    public DateOnly StartOf(int yearLabel) => new(yearLabel, YearStartMonth, 1);

    /// <summary>Day after the last day of a reporting year.</summary>
    public DateOnly EndExclusiveOf(int yearLabel) => StartOf(yearLabel + 1);

    /// <summary>Position of a date's month within its reporting year, 1–12 (1 == the start month).</summary>
    public int OrdinalMonthOf(DateOnly date)
    {
        var label = YearLabelOf(date);
        return (date.Year - label) * 12 + date.Month - YearStartMonth + 1;
    }

    /// <summary>The Gregorian month number (1–12) at a given ordinal position within the reporting year.</summary>
    public int CalendarMonthAt(int ordinalMonth) =>
        (YearStartMonth - 1 + (ordinalMonth - 1)) % 12 + 1;
}
