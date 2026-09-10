using System.Globalization;
using Finance.Domain.Reporting;

namespace Finance.Infrastructure;

/// <summary>
/// Typed access to the <c>config</c> key/value table — values that can change
/// but are not per-transaction. Nothing here is a compile-time constant
/// (spec §3): the year boundary in particular is read live at query time.
/// Implements <see cref="IReportingConfig"/> so the reporting ring can read the
/// year start without knowing it comes from SQLite.
/// </summary>
public sealed class ConfigStore : IReportingConfig
{
    public const string YearStartMonthKey = "year_start_month";

    private readonly SqliteDatabase _db;

    public ConfigStore(SqliteDatabase db)
    {
        _db = db;
    }

    public string? Get(string key)
    {
        using var connection = _db.OpenConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT value FROM config WHERE key = $key";
        cmd.Parameters.AddWithValue("$key", key);
        return cmd.ExecuteScalar() as string;
    }

    public void Set(string key, string value)
    {
        using var connection = _db.OpenConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText =
            "INSERT INTO config (key, value) VALUES ($key, $value) " +
            "ON CONFLICT(key) DO UPDATE SET value = excluded.value";
        cmd.Parameters.AddWithValue("$key", key);
        cmd.Parameters.AddWithValue("$value", value);
        cmd.ExecuteNonQuery();
    }

    /// <summary>
    /// Month that starts a reporting year. Absent config means the default, 1
    /// (January). A present-but-non-numeric value is corrupt and surfaces loudly.
    /// The 1–12 range itself is validated in exactly one place — the
    /// <see cref="Finance.Domain.Reporting.ReportingCalendar"/> constructor — so a
    /// stored 13 fails there, at the point of use, not silently here.
    /// </summary>
    public int GetYearStartMonth()
    {
        var raw = Get(YearStartMonthKey);
        if (raw is null)
            return 1;
        if (!int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var month))
            throw new InvalidOperationException(
                $"config.{YearStartMonthKey} = '{raw}' is not an integer.");
        return month;
    }

    /// <summary>
    /// Persists the reporting-year start month. Rejects out-of-range input up
    /// front by round-tripping it through <see cref="Finance.Domain.Reporting.ReportingCalendar"/>,
    /// the single owner of that invariant.
    /// </summary>
    public void SetYearStartMonth(int month)
    {
        _ = new Finance.Domain.Reporting.ReportingCalendar(month);
        Set(YearStartMonthKey, month.ToString(CultureInfo.InvariantCulture));
    }
}
