using Finance.Domain.Model;

namespace Finance.Domain.Persistence;

/// <summary>
/// Repository contracts live in Core so business logic (e.g.
/// <see cref="Sources.SourceRegistry"/>) depends on abstractions; the SQLite
/// implementations live in Finance.Infrastructure.
/// </summary>
public interface IBucketRepository
{
    IReadOnlyList<Bucket> GetAll();
    void Add(Bucket bucket);
}

public interface ICategoryRepository
{
    IReadOnlyList<Category> GetAll();
    Category? TryGet(string id);
    void Add(Category category);
}

public interface ISourceRepository
{
    IReadOnlyList<Source> GetAll();
    void SetStatus(string id, SourceStatus status);
}
