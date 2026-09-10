using System.Reflection;
using Microsoft.Data.Sqlite;

namespace Finance.Infrastructure;

/// <summary>
/// Owns the connection string to the single local SQLite file and applies the
/// embedded <c>schema.sql</c>. This is the only type that opens connections;
/// every caller goes through <see cref="OpenConnection"/> so foreign-key
/// enforcement (a per-connection pragma in SQLite) is never forgotten.
/// Plan step 2 / spec §4.3.
/// </summary>
public sealed class SqliteDatabase
{
    private const string SchemaResourceName = "Finance.Infrastructure.schema.sql";

    private readonly string _connectionString;

    public SqliteDatabase(string connectionString)
    {
        _connectionString = connectionString;
    }

    /// <summary>Database backed by a file on disk at <paramref name="path"/>.</summary>
    public static SqliteDatabase ForFile(string path)
    {
        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = path,
            ForeignKeys = true, // applied to every connection opened from this string
        };
        return new SqliteDatabase(builder.ConnectionString);
    }

    public string ConnectionString => _connectionString;

    public SqliteConnection OpenConnection()
    {
        var connection = new SqliteConnection(_connectionString);
        connection.Open();
        return connection;
    }

    /// <summary>
    /// Creates tables, indexes and seed rows if they are absent. Every statement
    /// in <c>schema.sql</c> is idempotent, so calling this repeatedly (on every
    /// host start-up, say) is a safe no-op.
    /// </summary>
    public void Bootstrap()
    {
        using var connection = OpenConnection();

        // journal_mode is persisted in the database header once set, and cannot
        // run inside a transaction -- so it goes first, on its own.
        using (var pragma = connection.CreateCommand())
        {
            pragma.CommandText = "PRAGMA journal_mode = WAL;";
            pragma.ExecuteNonQuery();
        }

        using var tx = connection.BeginTransaction();
        using (var cmd = connection.CreateCommand())
        {
            cmd.Transaction = tx;
            cmd.CommandText = ReadSchemaSql();
            cmd.ExecuteNonQuery();

            cmd.CommandText =
                "INSERT INTO schema_version (version) " +
                "SELECT 1 WHERE NOT EXISTS (SELECT 1 FROM schema_version);";
            cmd.ExecuteNonQuery();
        }

        tx.Commit();
    }

    private static string ReadSchemaSql()
    {
        var assembly = Assembly.GetExecutingAssembly();
        using var stream = assembly.GetManifestResourceStream(SchemaResourceName)
            ?? throw new InvalidOperationException(
                $"Embedded resource '{SchemaResourceName}' not found. " +
                "Check <EmbeddedResource Include=\"schema.sql\" /> in Finance.Infrastructure.csproj.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
