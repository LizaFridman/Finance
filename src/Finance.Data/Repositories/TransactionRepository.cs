using Finance.Core.Persistence;

namespace Finance.Data.Repositories;

public sealed class TransactionRepository : ITransactionRepository
{
    private readonly SqliteDatabase _db;

    public TransactionRepository(SqliteDatabase db) => _db = db;

    public bool InsertIfAbsent(IngestedTransaction transaction)
    {
        using var c = _db.OpenConnection();
        using var cmd = c.CreateCommand();
        // ON CONFLICT DO NOTHING makes a re-run a no-op (spec §7.1); ExecuteNonQuery
        // returns 1 when the row was written, 0 when the id already existed.
        cmd.CommandText =
            """
            INSERT INTO transactions (id, source_id, date, amount, merchant_raw)
            VALUES ($id, $source, $date, $amount, $merchant)
            ON CONFLICT(id) DO NOTHING
            """;
        cmd.Parameters.AddWithValue("$id", transaction.Id);
        cmd.Parameters.AddWithValue("$source", transaction.SourceId);
        cmd.Parameters.AddWithValue("$date", transaction.Date.ToString("yyyy-MM-dd"));
        cmd.Parameters.AddWithValue("$amount", transaction.AmountAgorot);
        cmd.Parameters.AddWithValue("$merchant", transaction.MerchantRaw);
        return cmd.ExecuteNonQuery() == 1;
    }

    public bool Exists(string id)
    {
        using var c = _db.OpenConnection();
        using var cmd = c.CreateCommand();
        cmd.CommandText = "SELECT EXISTS(SELECT 1 FROM transactions WHERE id = $id)";
        cmd.Parameters.AddWithValue("$id", id);
        return (long)cmd.ExecuteScalar()! == 1;
    }

    public int Count()
    {
        using var c = _db.OpenConnection();
        using var cmd = c.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM transactions";
        return (int)(long)cmd.ExecuteScalar()!;
    }

    private const string SelectColumns =
        "id, source_id, date, amount, merchant_raw, category_id, bucket_id, status";

    public TransactionDetail? GetById(string id)
    {
        using var c = _db.OpenConnection();
        using var cmd = c.CreateCommand();
        cmd.CommandText = $"SELECT {SelectColumns} FROM transactions WHERE id = $id";
        cmd.Parameters.AddWithValue("$id", id);
        using var r = cmd.ExecuteReader();
        return r.Read() ? Read(r) : null;
    }

    public IReadOnlyList<TransactionDetail> WhereCategoryIsNull()
    {
        using var c = _db.OpenConnection();
        using var cmd = c.CreateCommand();
        cmd.CommandText =
            $"SELECT {SelectColumns} FROM transactions WHERE category_id IS NULL ORDER BY date, id";
        using var r = cmd.ExecuteReader();
        var results = new List<TransactionDetail>();
        while (r.Read())
            results.Add(Read(r));
        return results;
    }

    public IReadOnlyList<TransactionDetail> Query(
        string? status = null,
        string? bucketId = null,
        DateOnly? from = null,
        DateOnly? to = null,
        bool categoryIsNull = false,
        bool bucketIsNull = false,
        int limit = 500)
    {
        using var c = _db.OpenConnection();
        using var cmd = c.CreateCommand();
        var sql = new System.Text.StringBuilder($"SELECT {SelectColumns} FROM transactions WHERE 1 = 1");

        if (status is not null) { sql.Append(" AND status = $status"); cmd.Parameters.AddWithValue("$status", status); }
        if (bucketId is not null) { sql.Append(" AND bucket_id = $bucket"); cmd.Parameters.AddWithValue("$bucket", bucketId); }
        if (from is { } f) { sql.Append(" AND date >= $from"); cmd.Parameters.AddWithValue("$from", f.ToString("yyyy-MM-dd")); }
        if (to is { } tt) { sql.Append(" AND date <= $to"); cmd.Parameters.AddWithValue("$to", tt.ToString("yyyy-MM-dd")); }
        if (categoryIsNull) sql.Append(" AND category_id IS NULL");
        if (bucketIsNull) sql.Append(" AND bucket_id IS NULL");

        sql.Append(" ORDER BY date, id LIMIT $limit");
        cmd.Parameters.AddWithValue("$limit", limit);
        cmd.CommandText = sql.ToString();

        using var r = cmd.ExecuteReader();
        var results = new List<TransactionDetail>();
        while (r.Read())
            results.Add(Read(r));
        return results;
    }

    public void SetCategory(string id, string categoryId)
    {
        using var c = _db.OpenConnection();
        using var cmd = c.CreateCommand();
        cmd.CommandText = "UPDATE transactions SET category_id = $cat WHERE id = $id";
        cmd.Parameters.AddWithValue("$cat", categoryId);
        cmd.Parameters.AddWithValue("$id", id);
        cmd.ExecuteNonQuery();
    }

    public void SetBucket(string id, string? bucketId)
    {
        using var c = _db.OpenConnection();
        using var cmd = c.CreateCommand();
        // One UPDATE, no file move — this is the whole point of the flat schema (spec §4.2).
        cmd.CommandText = "UPDATE transactions SET bucket_id = $bucket WHERE id = $id";
        cmd.Parameters.AddWithValue("$bucket", (object?)bucketId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$id", id);
        cmd.ExecuteNonQuery();
    }

    private static TransactionDetail Read(Microsoft.Data.Sqlite.SqliteDataReader r) => new(
        Id: r.GetString(0),
        SourceId: r.GetString(1),
        Date: DateOnly.ParseExact(r.GetString(2), "yyyy-MM-dd"),
        AmountAgorot: r.GetInt64(3),
        MerchantRaw: r.GetString(4),
        CategoryId: r.IsDBNull(5) ? null : r.GetString(5),
        BucketId: r.IsDBNull(6) ? null : r.GetString(6),
        Status: r.GetString(7));
}
