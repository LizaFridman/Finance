using Finance.Domain.Ingestion;
using Finance.Domain.Persistence;
using Finance.Domain.Sources;

namespace Finance.Application.Ingestion;

/// <summary>Outcome of an ingestion run. <see cref="Messages"/> is per-source progress detail.</summary>
public sealed record IngestionResult(
    int FilesProcessed,
    int Inserted,
    int SkippedDuplicates,
    int DroppedInstallments,
    IReadOnlyList<string> Messages);

/// <summary>
/// Walks every registered source's raw feed, parses whatever is there, and
/// inserts only transactions whose idempotency key is new (spec §7). Indifferent
/// to how a file arrived (scraper or hand-drop); an empty source is normal, not
/// an error (spec §7.3). Idempotent: a re-run only adds rows it hasn't seen.
/// </summary>
public sealed class IngestRawFeeds
{
    private readonly SourceRegistry _registry;
    private readonly IRawFeedFiles _files;
    private readonly IReadOnlyList<IRawFeedParser> _parsers;
    private readonly ITransactionRepository _transactions;
    private readonly InstallmentPolicy _installmentPolicy;

    public IngestRawFeeds(
        SourceRegistry registry,
        IRawFeedFiles files,
        IEnumerable<IRawFeedParser> parsers,
        ITransactionRepository transactions,
        InstallmentPolicy? installmentPolicy = null)
    {
        _registry = registry;
        _files = files;
        _parsers = parsers.ToList();
        _transactions = transactions;
        _installmentPolicy = installmentPolicy ?? new InstallmentPolicy();
    }

    public IngestionResult Execute()
    {
        int filesProcessed = 0, inserted = 0, skipped = 0, droppedInstallments = 0;
        var messages = new List<string>();

        // Every registered source, dormant included — a dormant source's back
        // catalogue is still part of the baseline (spec §6).
        foreach (var source in _registry.SourcesForBaselineImport())
        {
            var feedFiles = _files.List(source.Id);
            if (feedFiles.Count == 0)
            {
                messages.Add($"source '{source.Id}': no files (expected if dormant or not yet synced)");
                continue;
            }

            foreach (var file in feedFiles)
            {
                var parser = _parsers.FirstOrDefault(p => p.CanParse(file.Name));
                if (parser is null)
                {
                    messages.Add($"source '{source.Id}': no parser for '{file.Name}', skipped");
                    continue;
                }

                filesProcessed++;
                var parsed = parser.Parse(file.Path, source.Id);
                foreach (var error in parsed.Errors)
                    messages.Add($"source '{source.Id}': {error}");

                var (kept, dropped) = _installmentPolicy.Filter(parsed.Records);
                droppedInstallments += dropped;

                foreach (var record in kept)
                {
                    var row = new IngestedTransaction(
                        Id: IdempotencyKey.For(record),
                        SourceId: record.SourceId,
                        Date: record.Date,
                        Amount: record.Amount,
                        MerchantRaw: record.MerchantRaw);

                    if (_transactions.InsertIfAbsent(row))
                        inserted++;
                    else
                        skipped++;
                }
            }
        }

        return new IngestionResult(filesProcessed, inserted, skipped, droppedInstallments, messages);
    }
}
