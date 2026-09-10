namespace Finance.Domain.Ingestion;

/// <summary>
/// Parses one raw-feed file into <see cref="TransactionRecord"/>s. Implementations
/// are format-specific and live in the infrastructure ring; the ingestor picks the
/// first whose <see cref="CanParse"/> matches. The pipeline does not care whether
/// the file came from the scraper or was dropped in by hand (spec §7.3).
/// </summary>
public interface IRawFeedParser
{
    bool CanParse(string fileName);
    IReadOnlyList<TransactionRecord> Parse(string filePath, string sourceId);
}

/// <summary>
/// Placeholder for the deferred statement parsers (Cal/Max xlsx, and the
/// water/electricity/gas/vaad/Partner/Dor-Gaz PDFs — spec §5). It claims the
/// binary formats so an accidental drop fails loudly instead of being silently
/// skipped. Implementing these is the follow-up to this scaffold; see the plan's
/// "deferred" list and docs/missing-data-report.md.
/// </summary>
public sealed class NotImplementedRawFeedParser : IRawFeedParser
{
    private static readonly string[] Extensions = { ".pdf", ".xlsx", ".xlsm", ".xls" };

    public bool CanParse(string fileName) =>
        Extensions.Any(ext => fileName.EndsWith(ext, StringComparison.OrdinalIgnoreCase));

    public IReadOnlyList<TransactionRecord> Parse(string filePath, string sourceId) =>
        throw new NotSupportedException(
            $"No parser for '{Path.GetFileName(filePath)}' yet. Statement/PDF parsing is a " +
            "deferred follow-up (see the plan). Drop scraper JSON here for now, or add the " +
            "parser and register it ahead of this one.");
}
