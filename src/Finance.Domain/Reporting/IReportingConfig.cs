namespace Finance.Domain.Reporting;

/// <summary>
/// Supplies the configurable reporting-year start month (spec §3), read live at
/// query time so a change takes effect without a restart. The 1–12 range is
/// validated by <see cref="ReportingCalendar"/>, not here.
/// </summary>
public interface IReportingConfig
{
    int GetYearStartMonth();
}
