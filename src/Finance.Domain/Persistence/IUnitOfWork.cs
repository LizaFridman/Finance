namespace Finance.Domain.Persistence;

/// <summary>
/// Runs a group of repository writes as one atomic unit — all of them commit, or
/// none do. Used where a single logical change spans more than one repository
/// (e.g. confirming a category touches the transaction, the taxonomy and the
/// merchant dictionary).
/// </summary>
public interface IUnitOfWork
{
    void Execute(Action work);
}
