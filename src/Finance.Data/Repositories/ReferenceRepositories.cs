using Finance.Core.Model;
using Finance.Core.Persistence;

namespace Finance.Data.Repositories;

public sealed class BucketRepository : IBucketRepository
{
    private readonly SqliteDatabase _db;

    public BucketRepository(SqliteDatabase db) => _db = db;

    public IReadOnlyList<Bucket> GetAll()
    {
        using var c = _db.OpenConnection();
        using var cmd = c.CreateCommand();
        cmd.CommandText = "SELECT id, label FROM buckets ORDER BY id";
        using var r = cmd.ExecuteReader();
        var results = new List<Bucket>();
        while (r.Read())
            results.Add(new Bucket(r.GetString(0), r.GetString(1)));
        return results;
    }

    public void Add(Bucket bucket)
    {
        using var c = _db.OpenConnection();
        using var cmd = c.CreateCommand();
        cmd.CommandText = "INSERT INTO buckets (id, label) VALUES ($id, $label)";
        cmd.Parameters.AddWithValue("$id", bucket.Id);
        cmd.Parameters.AddWithValue("$label", bucket.Label);
        cmd.ExecuteNonQuery();
    }
}

public sealed class CategoryRepository : ICategoryRepository
{
    private readonly SqliteDatabase _db;

    public CategoryRepository(SqliteDatabase db) => _db = db;

    public IReadOnlyList<Category> GetAll()
    {
        using var c = _db.OpenConnection();
        using var cmd = c.CreateCommand();
        cmd.CommandText = "SELECT id, label FROM categories ORDER BY id";
        using var r = cmd.ExecuteReader();
        var results = new List<Category>();
        while (r.Read())
            results.Add(new Category(r.GetString(0), r.GetString(1)));
        return results;
    }

    public Category? TryGet(string id)
    {
        using var c = _db.OpenConnection();
        using var cmd = c.CreateCommand();
        cmd.CommandText = "SELECT id, label FROM categories WHERE id = $id";
        cmd.Parameters.AddWithValue("$id", id);
        using var r = cmd.ExecuteReader();
        return r.Read() ? new Category(r.GetString(0), r.GetString(1)) : null;
    }

    public void Add(Category category)
    {
        using var c = _db.OpenConnection();
        using var cmd = c.CreateCommand();
        // INSERT OR IGNORE: the taxonomy grows from many independent Layer 2
        // decisions; two confirmations of the same new category must not collide.
        cmd.CommandText = "INSERT OR IGNORE INTO categories (id, label) VALUES ($id, $label)";
        cmd.Parameters.AddWithValue("$id", category.Id);
        cmd.Parameters.AddWithValue("$label", category.Label);
        cmd.ExecuteNonQuery();
    }
}

public sealed class SourceRepository : ISourceRepository
{
    private readonly SqliteDatabase _db;

    public SourceRepository(SqliteDatabase db) => _db = db;

    public IReadOnlyList<Source> GetAll()
    {
        using var c = _db.OpenConnection();
        using var cmd = c.CreateCommand();
        cmd.CommandText = "SELECT id, status FROM sources ORDER BY id";
        using var r = cmd.ExecuteReader();
        var results = new List<Source>();
        while (r.Read())
            results.Add(new Source(r.GetString(0), ParseStatus(r.GetString(1))));
        return results;
    }

    public void SetStatus(string id, SourceStatus status)
    {
        using var c = _db.OpenConnection();
        using var cmd = c.CreateCommand();
        cmd.CommandText = "UPDATE sources SET status = $status WHERE id = $id";
        cmd.Parameters.AddWithValue("$status", status.ToString().ToLowerInvariant());
        cmd.Parameters.AddWithValue("$id", id);
        cmd.ExecuteNonQuery();
    }

    private static SourceStatus ParseStatus(string raw) => raw switch
    {
        "active" => SourceStatus.Active,
        "dormant" => SourceStatus.Dormant,
        _ => throw new InvalidOperationException($"Unknown source status '{raw}'."),
    };
}
