using System.Globalization;
using System.Text;
using Finance.Domain.Reporting;

namespace Finance.Infrastructure.Reporting;

/// <summary>
/// The one live <c>SELECT</c> behind every time-series report. Applies the
/// <see cref="SeriesFilter"/> as SQL; everything past this point is in-memory math
/// in <see cref="TimeSeriesCalculator"/>. Nothing is cached — each call reads the
/// current table state (spec §4.2).
/// </summary>
public sealed class TransactionRowReader : ITransactionRowReader
{
    private readonly SqliteDatabase _db;

    public TransactionRowReader(SqliteDatabase db) => _db = db;

    public IReadOnlyList<TransactionRow> Read(DateOnly fromInclusive, DateOnly toInclusive, SeriesFilter filter)
    {
        using var connection = _db.OpenConnection();
        using var cmd = connection.CreateCommand();

        var sql = new StringBuilder(
            "SELECT date, amount, bucket_id, category_id FROM transactions " +
            "WHERE date >= $from AND date <= $to");
        cmd.Parameters.AddWithValue("$from", fromInclusive.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        cmd.Parameters.AddWithValue("$to", toInclusive.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));

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
        var rows = new List<TransactionRow>();
        while (reader.Read())
        {
            rows.Add(new TransactionRow(
                DateOnly.ParseExact(reader.GetString(0), "yyyy-MM-dd", CultureInfo.InvariantCulture),
                reader.GetInt64(1),
                reader.IsDBNull(2) ? null : reader.GetString(2),
                reader.IsDBNull(3) ? null : reader.GetString(3)));
        }
        return rows;
    }
}
