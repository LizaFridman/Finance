using System.Globalization;

namespace Finance.Infrastructure;

/// <summary>
/// Typed access to the <c>config</c> key/value table — values that can change
/// but are not per-transaction. Nothing here is a compile-time constant
/// (spec §3): the year boundary in particular is read live at query time.
/// </summary>
public sealed class ConfigStore
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
    /// Month (1–12) that starts a reporting year. Drives yearly rollups and the
    /// cumulative-within-year reset. Defaults to 1 (January) via the schema seed.
    /// </summary>
    public int GetYearStartMonth()
    {
        var raw = Get(YearStartMonthKey);
        return raw is not null
            && int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var month)
            ? month : 1;
    }

    public void SetYearStartMonth(int month)
    {
        if (month is < 1 or > 12)
            throw new ArgumentOutOfRangeException(
                nameof(month), month, "Year start month must be between 1 and 12.");
        Set(YearStartMonthKey, month.ToString(CultureInfo.InvariantCulture));
    }
}
