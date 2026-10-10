using System.Collections.Generic;
using System.Linq;
using Finance.Infrastructure;
using Microsoft.Data.Sqlite;

namespace Finance.Tests.Infrastructure;

public class SchemaBootstrapTests
{
    [Fact]
    public void Bootstrap_creates_all_expected_tables()
    {
        using var t = new TempDatabase();

        var tables = QueryStrings(t.Db,
            "SELECT name FROM sqlite_master WHERE type = 'table'");

        foreach (var expected in new[]
                 {
                     "buckets", "categories", "sources", "merchant_dictionary",
                     "transactions", "config", "schema_version"
                 })
        {
            Assert.Contains(expected, tables);
        }
    }

    [Fact]
    public void Bootstrap_enables_WAL()
    {
        using var t = new TempDatabase();

        var mode = QueryStrings(t.Db, "PRAGMA journal_mode").Single();

        Assert.Equal("wal", mode.ToLowerInvariant());
    }

    [Fact]
    public void Bootstrap_enforces_foreign_keys()
    {
        using var t = new TempDatabase();
        using var c = t.Db.OpenConnection();
        using var cmd = c.CreateCommand();
        cmd.CommandText =
            "INSERT INTO transactions (id, source_id, date, amount, merchant_raw) " +
            "VALUES ('x', 'no-such-source', '2025-01-01', -100, 'test')";

        var ex = Assert.Throws<SqliteException>(() => cmd.ExecuteNonQuery());
        Assert.Contains("FOREIGN KEY", ex.Message, System.StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Bootstrap_is_idempotent()
    {
        using var t = new TempDatabase();

        t.Db.Bootstrap();
        t.Db.Bootstrap();

        Assert.Equal(3, Scalar(t.Db, "SELECT COUNT(*) FROM buckets"));
        Assert.Equal(8, Scalar(t.Db, "SELECT COUNT(*) FROM sources"));
        Assert.Equal(1, Scalar(t.Db, "SELECT COUNT(*) FROM config WHERE key = 'year_start_month'"));
    }

    [Fact]
    public void Bootstrap_seeds_only_known_buckets_and_sources()
    {
        using var t = new TempDatabase();

        Assert.Equal(
            new[] { "personal_akumu", "personal_liza", "shared" },
            QueryStrings(t.Db, "SELECT id FROM buckets ORDER BY id"));

        var sources = QueryPairs(t.Db, "SELECT id, status FROM sources ORDER BY id");
        Assert.Equal("active", sources["cal"]);
        Assert.Equal("active", sources["leumi"]);
        Assert.Equal("dormant", sources["max"]);
        foreach (var bill in new[] { "water", "electricity", "gas", "vaad", "partner" })
            Assert.Equal("active", sources[bill]);
    }

    [Fact]
    public void Categories_and_merchant_dictionary_start_empty()
    {
        using var t = new TempDatabase();

        Assert.Equal(0, Scalar(t.Db, "SELECT COUNT(*) FROM categories"));
        Assert.Equal(0, Scalar(t.Db, "SELECT COUNT(*) FROM merchant_dictionary"));
    }

    // --- helpers -----------------------------------------------------------

    private static List<string> QueryStrings(SqliteDatabase db, string sql)
    {
        using var c = db.OpenConnection();
        using var cmd = c.CreateCommand();
        cmd.CommandText = sql;
        using var r = cmd.ExecuteReader();
        var results = new List<string>();
        while (r.Read())
            results.Add(r.GetString(0));
        return results;
    }

    private static Dictionary<string, string> QueryPairs(SqliteDatabase db, string sql)
    {
        using var c = db.OpenConnection();
        using var cmd = c.CreateCommand();
        cmd.CommandText = sql;
        using var r = cmd.ExecuteReader();
        var results = new Dictionary<string, string>();
        while (r.Read())
            results[r.GetString(0)] = r.GetString(1);
        return results;
    }

    private static long Scalar(SqliteDatabase db, string sql)
    {
        using var c = db.OpenConnection();
        using var cmd = c.CreateCommand();
        cmd.CommandText = sql;
        return (long)cmd.ExecuteScalar()!;
    }
}
