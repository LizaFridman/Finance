using Finance.Core.Model;
using Finance.Core.Persistence;

namespace Finance.Core.Categorization;

/// <summary>
/// Drives categorization. Layer 2 is an interactive review (the Claude Code loop
/// / the Host's review endpoint): a human confirms a category for a transaction,
/// and that decision is written back to Layer 1 so identical merchants resolve
/// automatically afterwards (spec §10.2). The category taxonomy is created here,
/// on demand — nothing is seeded from the old system.
/// </summary>
public sealed class CategorizationService
{
    private readonly ITransactionRepository _transactions;
    private readonly ICategoryRepository _categories;
    private readonly IMerchantDictionary _dictionary;
    private readonly MerchantMatcher _matcher;

    public CategorizationService(
        ITransactionRepository transactions,
        ICategoryRepository categories,
        IMerchantDictionary dictionary)
    {
        _transactions = transactions;
        _categories = categories;
        _dictionary = dictionary;
        _matcher = new MerchantMatcher(dictionary);
    }

    /// <summary>
    /// Records a confirmed Layer 2 decision: creates the category if it is new,
    /// sets it on the transaction, and teaches Layer 1 the merchant→category
    /// mapping (keyed on the exact scraped string).
    /// </summary>
    public void ConfirmCategory(string transactionId, string categoryId, string? categoryLabel = null)
    {
        var transaction = _transactions.GetById(transactionId)
            ?? throw new InvalidOperationException($"No transaction '{transactionId}'.");

        _categories.Add(new Category(categoryId, categoryLabel ?? categoryId));
        _transactions.SetCategory(transactionId, categoryId);
        _dictionary.Upsert(transaction.MerchantRaw, categoryId);
    }

    /// <summary>
    /// Applies Layer 1 to a still-uncategorized transaction. Returns true iff a
    /// category was assigned. Used to sweep the backlog after each confirmation.
    /// </summary>
    public bool TryAutoCategorize(string transactionId)
    {
        var transaction = _transactions.GetById(transactionId);
        if (transaction is null || transaction.CategoryId is not null)
            return false;

        var categoryId = _matcher.Resolve(transaction.MerchantRaw);
        if (categoryId is null)
            return false;

        _transactions.SetCategory(transactionId, categoryId);
        return true;
    }

    /// <summary>Sweeps every uncategorized transaction through Layer 1. Returns how many were resolved.</summary>
    public int AutoCategorizeBacklog()
    {
        var resolved = 0;
        foreach (var transaction in _transactions.WhereCategoryIsNull())
        {
            if (TryAutoCategorize(transaction.Id))
                resolved++;
        }
        return resolved;
    }
}
