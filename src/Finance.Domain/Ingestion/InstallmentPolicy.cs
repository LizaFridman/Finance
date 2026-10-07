namespace Finance.Domain.Ingestion;

/// <summary>
/// Installment rule (spec §8): the full original amount is recorded once, on the
/// first installment's date; every later installment of the same series is
/// discarded. The scraper's <c>combineInstallments: true</c> already does this
/// upstream — this class is the C# safety net that keeps the guarantee even for
/// hand-dropped files or a future parser that doesn't combine.
/// </summary>
public sealed class InstallmentPolicy
{
    public bool Keep(TransactionRecord record) =>
        record.Installments is null || record.Installments.Number == 1;

    public (IReadOnlyList<TransactionRecord> Kept, int Dropped) Filter(
        IEnumerable<TransactionRecord> records)
    {
        var kept = new List<TransactionRecord>();
        var dropped = 0;
        foreach (var record in records)
        {
            if (Keep(record))
                kept.Add(record);
            else
                dropped++;
        }
        return (kept, dropped);
    }
}
