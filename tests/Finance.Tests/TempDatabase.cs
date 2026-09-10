using System;
using System.IO;
using Finance.Infrastructure;

namespace Finance.Tests;

/// <summary>
/// A throwaway on-disk SQLite database for a single test. On-disk (not
/// <c>:memory:</c>) because the plan requires asserting WAL mode, which
/// in-memory databases do not support.
/// </summary>
public sealed class TempDatabase : IDisposable
{
    public string Path { get; }
    public SqliteDatabase Db { get; }

    public TempDatabase(bool bootstrap = true)
    {
        Path = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            $"finance-test-{Guid.NewGuid():N}.db");
        Db = SqliteDatabase.ForFile(Path);
        if (bootstrap)
            Db.Bootstrap();
    }

    public void Dispose()
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        foreach (var suffix in new[] { "", "-wal", "-shm" })
        {
            try { File.Delete(Path + suffix); }
            catch (IOException) { /* best effort on Windows file locks */ }
        }
    }
}
