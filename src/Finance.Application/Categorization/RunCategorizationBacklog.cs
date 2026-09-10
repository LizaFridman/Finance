using Finance.Domain.Categorization;
using Finance.Domain.Persistence;

namespace Finance.Application.Categorization;

/// <summary>
/// Sweeps every still-uncategorized transaction through Layer 1 (spec §10.2), run
/// after each confirmation so a new mapping is applied to the existing backlog.
/// Layer 1 is read once for the whole sweep, and the writes commit as one unit.
/// </summary>
public sealed class RunCategorizationBacklog
{
    private readonly ITransactionRepository _transactions;
    private readonly IMerchantDictionary _dictionary;
    private readonly IUnitOfWork _unitOfWork;

    public RunCategorizationBacklog(
        ITransactionRepository transactions, IMerchantDictionary dictionary, IUnitOfWork unitOfWork)
    {
        _transactions = transactions;
        _dictionary = dictionary;
        _unitOfWork = unitOfWork;
    }

    /// <summary>Returns how many transactions were resolved.</summary>
    public int Execute()
    {
        var matcher = MerchantMatcher.FromDictionary(_dictionary);
        var resolved = 0;

        _unitOfWork.Execute(() =>
        {
            foreach (var transaction in _transactions.WhereCategoryIsNull())
            {
                var categoryId = matcher.Resolve(transaction.MerchantRaw);
                if (categoryId is null)
                    continue;
                _transactions.SetCategory(transaction.Id, categoryId);
                resolved++;
            }
        });

        return resolved;
    }
}
