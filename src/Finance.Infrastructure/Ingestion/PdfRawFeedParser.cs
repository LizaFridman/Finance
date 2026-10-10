using Finance.Domain.Ingestion;

namespace Finance.Infrastructure.Ingestion;

/// <summary>
/// The ingestor picks a parser by file name alone, and two kinds of PDF share the
/// <c>.pdf</c> extension: Cal's monthly statement (many transactions) and the
/// utility bills (one charge each). The source folder tells them apart.
/// </summary>
public sealed class PdfRawFeedParser(CalStatementPdfParser calStatements, UtilityBillPdfParser bills)
    : IRawFeedParser
{
    private const string CalSourceId = "cal";

    public bool CanParse(string fileName) =>
        fileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase);

    public RawFeedParseResult Parse(string filePath, string sourceId) =>
        sourceId == CalSourceId
            ? calStatements.Parse(filePath, sourceId)
            : bills.Parse(filePath, sourceId);
}
