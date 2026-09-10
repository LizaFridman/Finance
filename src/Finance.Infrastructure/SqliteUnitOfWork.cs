using Finance.Domain.Persistence;

namespace Finance.Infrastructure;

/// <summary>
/// <see cref="IUnitOfWork"/> over the single SQLite connection: delegates to
/// <see cref="SqliteDatabase.InTransaction"/>, so every repository call made
/// inside <see cref="Execute"/> shares one connection and one transaction.
/// </summary>
public sealed class SqliteUnitOfWork : IUnitOfWork
{
    private readonly SqliteDatabase _db;

    public SqliteUnitOfWork(SqliteDatabase db) => _db = db;

    public void Execute(Action work) => _db.InTransaction(work);
}
