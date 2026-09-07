namespace Finance.Core.Persistence;

/// <summary>
/// A transaction as it first lands in the store: keyed, but with no category and
/// no bucket yet (those are assigned in later phases). Amount is signed agorot.
/// </summary>
public sealed record IngestedTransaction(
    string Id,
    string SourceId,
    DateOnly Date,
    long AmountAgorot,
    string MerchantRaw);

/// <summary>Full stored view of a transaction, including the fields assigned in later phases.</summary>
public sealed record TransactionDetail(
    string Id,
    string SourceId,
    DateOnly Date,
    long AmountAgorot,
    string MerchantRaw,
    string? CategoryId,
    string? BucketId,
    string Status);

public interface ITransactionRepository
{
    /// <summary>Inserts the row unless its id already exists. Returns true iff a row was written.</summary>
    bool InsertIfAbsent(IngestedTransaction transaction);

    bool Exists(string id);

    int Count();

    TransactionDetail? GetById(string id);

    /// <summary>Transactions still awaiting a Layer 1/2 category (spec §10.2 — at the start, all of them).</summary>
    IReadOnlyList<TransactionDetail> WhereCategoryIsNull();

    /// <summary>Filtered read for the API / review loops. All arguments optional.</summary>
    IReadOnlyList<TransactionDetail> Query(
        string? status = null,
        string? bucketId = null,
        DateOnly? from = null,
        DateOnly? to = null,
        bool categoryIsNull = false,
        bool bucketIsNull = false,
        int limit = 500);

    void SetCategory(string id, string categoryId);

    /// <summary>Assigns (or clears, when null) a transaction's bucket. Phase 3 (spec §4.2).</summary>
    void SetBucket(string id, string? bucketId);
}
