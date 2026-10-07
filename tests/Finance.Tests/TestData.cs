using System;
using Finance.Domain;
using Finance.Infrastructure;

namespace Finance.Tests;

/// <summary>Helpers for seeding transactions straight into a test database.</summary>
public static class TestData
{
    public static void InsertTx(
        SqliteDatabase db,
        string id,
        string date,
        decimal shekels,
        string? bucket = null,
        string? category = null,
        string source = "leumi")
    {
        using var c = db.OpenConnection();
        using var cmd = c.CreateCommand();
        cmd.CommandText =
            """
            INSERT INTO transactions (id, source_id, date, amount, merchant_raw, bucket_id, category_id)
            VALUES ($id, $src, $date, $amount, $m, $bucket, $cat)
            """;
        cmd.Parameters.AddWithValue("$id", id);
        cmd.Parameters.AddWithValue("$src", source);
        cmd.Parameters.AddWithValue("$date", date);
        cmd.Parameters.AddWithValue("$amount", Money.FromShekels(shekels).Agorot);
        cmd.Parameters.AddWithValue("$m", $"merchant-{id}");
        cmd.Parameters.AddWithValue("$bucket", (object?)bucket ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$cat", (object?)category ?? DBNull.Value);
        cmd.ExecuteNonQuery();
    }

    public static void MoveBucket(SqliteDatabase db, string id, string? bucket)
    {
        using var c = db.OpenConnection();
        using var cmd = c.CreateCommand();
        cmd.CommandText = "UPDATE transactions SET bucket_id = $b WHERE id = $id";
        cmd.Parameters.AddWithValue("$b", (object?)bucket ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$id", id);
        cmd.ExecuteNonQuery();
    }
}
