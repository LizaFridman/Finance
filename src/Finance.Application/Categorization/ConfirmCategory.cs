using Finance.Domain;
using Finance.Domain.Categorization;
using Finance.Domain.Model;
using Finance.Domain.Persistence;

namespace Finance.Application.Categorization;

/// <summary>
/// Records a confirmed Layer 2 decision (spec §10.2): make sure the category
/// exists, set it on the transaction, and teach Layer 1 the merchant→category
/// mapping so identical merchants resolve automatically afterwards. The three
/// writes commit together or not at all.
/// </summary>
public sealed class ConfirmCategory
{
    private readonly ITransactionRepository _transactions;
    private readonly ICategoryRepository _categories;
    private readonly IMerchantDictionary _dictionary;
    private readonly IUnitOfWork _unitOfWork;

    public ConfirmCategory(
        ITransactionRepository transactions,
        ICategoryRepository categories,
        IMerchantDictionary dictionary,
        IUnitOfWork unitOfWork)
    {
        _transactions = transactions;
        _categories = categories;
        _dictionary = dictionary;
        _unitOfWork = unitOfWork;
    }

    public void Execute(string transactionId, string categoryId, string? categoryLabel = null)
    {
        if (string.IsNullOrWhiteSpace(transactionId))
            throw new ArgumentException("Transaction id is required.", nameof(transactionId));
        if (string.IsNullOrWhiteSpace(categoryId))
            throw new ArgumentException("Category id is required.", nameof(categoryId));

        var transaction = _transactions.GetById(transactionId)
            ?? throw new NotFoundException("Transaction", transactionId);

        _unitOfWork.Execute(() =>
        {
            if (_categories.TryGet(categoryId) is null)
                _categories.Add(new Category(categoryId, (categoryLabel ?? categoryId).Trim()));
            _transactions.SetCategory(transactionId, categoryId);
            _dictionary.Upsert(transaction.MerchantRaw, categoryId);
        });
    }
}
