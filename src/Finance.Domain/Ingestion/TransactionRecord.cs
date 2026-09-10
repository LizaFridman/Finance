namespace Finance.Domain.Ingestion;

/// <summary>Installment position within a series, as reported by the source.</summary>
public sealed record InstallmentInfo(int Number, int Total);

/// <summary>
/// One transaction as produced by a parser (scraper JSON today; statement
/// parsers later). Source-shaped and pre-persistence: no category, no bucket,
/// no idempotency key yet. Amount is signed agorot (negative = outflow).
/// </summary>
public sealed record TransactionRecord(
    string SourceId,
    DateOnly Date,
    long AmountAgorot,
    string MerchantRaw,
    string? NativeId = null,
    InstallmentInfo? Installments = null);
