namespace Finance.Domain.Ingestion;

/// <summary>
/// What a parser got out of one file: the records it could read, plus a line per
/// row it could not (bad date, missing merchant, …). A single malformed row does
/// not fail the whole file — the errors travel back in the ingestion result.
/// </summary>
public sealed record RawFeedParseResult(
    IReadOnlyList<TransactionRecord> Records,
    IReadOnlyList<string> Errors)
{
    public static RawFeedParseResult FromRecords(IReadOnlyList<TransactionRecord> records) =>
        new(records, []);
}

/// <summary>
/// Parses one raw-feed file into <see cref="TransactionRecord"/>s. Implementations
/// are format-specific and live in the infrastructure ring; the ingestor picks the
/// first whose <see cref="CanParse"/> matches. The pipeline does not care whether
/// the file came from the scraper or was dropped in by hand (spec §7.3).
/// </summary>
public interface IRawFeedParser
{
    bool CanParse(string fileName);
    RawFeedParseResult Parse(string filePath, string sourceId);
}

/// <summary>
/// Placeholder for the one format with no parser yet: macro-enabled <c>.xlsm</c>
/// (Cal's <c>כאל 2025.xlsm</c> workbook — its layout is unverified, spec §5).
/// <c>.json</c>, <c>.xls</c> (Leumi), <c>.xlsx</c> (Max) and <c>.pdf</c> (utility
/// bills) all have real parsers registered ahead of this one. It claims
/// <c>.xlsm</c> so an accidental drop fails loudly instead of being silently
/// skipped; see docs/missing-data-report.md.
/// </summary>
public sealed class NotImplementedRawFeedParser : IRawFeedParser
{
    private static readonly string[] Extensions = { ".xlsm" };

    public bool CanParse(string fileName) =>
        Extensions.Any(ext => fileName.EndsWith(ext, StringComparison.OrdinalIgnoreCase));

    public RawFeedParseResult Parse(string filePath, string sourceId) =>
        throw new NotSupportedException(
            $"No parser for '{Path.GetFileName(filePath)}' yet. Macro-enabled .xlsm statements " +
            "aren't handled — re-save as .xlsx if it matches the Max layout, or add a parser " +
            "and register it ahead of this one.");
}
