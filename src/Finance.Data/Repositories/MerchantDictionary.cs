using Finance.Core.Categorization;

namespace Finance.Data.Repositories;

public sealed class MerchantDictionary : IMerchantDictionary
{
    private readonly SqliteDatabase _db;

    public MerchantDictionary(SqliteDatabase db) => _db = db;

    public string? Resolve(string merchantRaw)
    {
        using var c = _db.OpenConnection();
        using var cmd = c.CreateCommand();
        cmd.CommandText = "SELECT category_id FROM merchant_dictionary WHERE merchant_raw = $m";
        cmd.Parameters.AddWithValue("$m", merchantRaw.Trim());
        return cmd.ExecuteScalar() as string;
    }

    public void Upsert(string merchantRaw, string categoryId)
    {
        using var c = _db.OpenConnection();
        using var cmd = c.CreateCommand();
        cmd.CommandText =
            """
            INSERT INTO merchant_dictionary (merchant_raw, category_id, confirmed_at)
            VALUES ($m, $c, datetime('now'))
            ON CONFLICT(merchant_raw) DO UPDATE SET
              category_id = excluded.category_id,
              confirmed_at = excluded.confirmed_at
            """;
        cmd.Parameters.AddWithValue("$m", merchantRaw.Trim());
        cmd.Parameters.AddWithValue("$c", categoryId);
        cmd.ExecuteNonQuery();
    }

    public IReadOnlyList<KeyValuePair<string, string>> Entries()
    {
        using var c = _db.OpenConnection();
        using var cmd = c.CreateCommand();
        cmd.CommandText = "SELECT merchant_raw, category_id FROM merchant_dictionary";
        using var r = cmd.ExecuteReader();
        var results = new List<KeyValuePair<string, string>>();
        while (r.Read())
            results.Add(new KeyValuePair<string, string>(r.GetString(0), r.GetString(1)));
        return results;
    }
}
