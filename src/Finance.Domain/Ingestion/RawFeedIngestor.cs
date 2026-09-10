using Finance.Domain.Persistence;
using Finance.Domain.Sources;

namespace Finance.Domain.Ingestion;

public sealed record IngestionResult(
    int FilesProcessed,
    int Inserted,
    int SkippedDuplicates,
    int DroppedInstallments,
    IReadOnlyList<string> Messages);

/// <summary>
/// Walks <c>Raw Data Feed/&lt;source&gt;/</c>, parses whatever is there, and inserts
/// only transactions whose idempotency key is new. It is deliberately indifferent
/// to how a file arrived (scraper or manual) and treats an empty source folder as
/// normal, not an error (spec §7.3).
/// </summary>
public sealed class RawFeedIngestor
{
    private readonly SourceRegistry _registry;
    private readonly ITransactionRepository _transactions;
    private readonly IReadOnlyList<IRawFeedParser> _parsers;
    private readonly InstallmentPolicy _installmentPolicy;

    public RawFeedIngestor(
        SourceRegistry registry,
        ITransactionRepository transactions,
        IEnumerable<IRawFeedParser> parsers,
        InstallmentPolicy? installmentPolicy = null)
    {
        _registry = registry;
        _transactions = transactions;
        _parsers = parsers.ToList();
        _installmentPolicy = installmentPolicy ?? new InstallmentPolicy();
    }

    public IngestionResult Ingest(string rawFeedRoot)
    {
        int files = 0, inserted = 0, skipped = 0, droppedInstallments = 0;
        var messages = new List<string>();

        // Every registered source, dormant included — a dormant source's back
        // catalogue is still part of the baseline (spec §6).
        foreach (var source in _registry.SourcesForBaselineImport())
        {
            var folder = Path.Combine(rawFeedRoot, source.Id);
            if (!Directory.Exists(folder))
            {
                messages.Add($"source '{source.Id}': folder missing, no files (expected if dormant)");
                continue;
            }

            var feedFiles = Directory
                .EnumerateFiles(folder)
                .Where(f => !Path.GetFileName(f).StartsWith('.'))
                .OrderBy(f => f, StringComparer.Ordinal)
                .ToList();

            if (feedFiles.Count == 0)
            {
                messages.Add($"source '{source.Id}': no files (expected if dormant or not yet synced)");
                continue;
            }

            foreach (var file in feedFiles)
            {
                var fileName = Path.GetFileName(file);
                var parser = _parsers.FirstOrDefault(p => p.CanParse(fileName));
                if (parser is null)
                {
                    messages.Add($"source '{source.Id}': no parser for '{fileName}', skipped");
                    continue;
                }

                files++;
                var parsed = parser.Parse(file, source.Id);
                var (kept, dropped) = _installmentPolicy.Filter(parsed);
                droppedInstallments += dropped;

                foreach (var record in kept)
                {
                    var row = new IngestedTransaction(
                        Id: IdempotencyKey.For(record),
                        SourceId: record.SourceId,
                        Date: record.Date,
                        AmountAgorot: record.AmountAgorot,
                        MerchantRaw: record.MerchantRaw);

                    if (_transactions.InsertIfAbsent(row))
                        inserted++;
                    else
                        skipped++;
                }
            }
        }

        return new IngestionResult(files, inserted, skipped, droppedInstallments, messages);
    }
}
