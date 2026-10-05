using System.Text.Json;
using System.Text.Json.Serialization;

namespace Finance.Core.Ingestion;

/// <summary>
/// Parses one raw-feed file into <see cref="TransactionRecord"/>s. Implementations
/// are format-specific; the ingestor picks the first whose <see cref="CanParse"/>
/// matches. The pipeline does not care whether the file came from the scraper or
/// was dropped in by hand (spec §7.3).
/// </summary>
public interface IRawFeedParser
{
    bool CanParse(string fileName);
    IReadOnlyList<TransactionRecord> Parse(string filePath, string sourceId);
}

/// <summary>
/// Reads the normalized JSON array emitted by the Node scraper wrapper
/// (<c>scraper/</c>). Cal/Max statement workbooks and utility bill PDFs are
/// handled by <see cref="XlsxStatementParser"/> and
/// <see cref="UtilityBillPdfParser"/> respectively.
/// </summary>
public sealed class ScraperJsonParser : IRawFeedParser
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
    };

    public bool CanParse(string fileName) =>
        fileName.EndsWith(".json", StringComparison.OrdinalIgnoreCase);

    public IReadOnlyList<TransactionRecord> Parse(string filePath, string sourceId)
    {
        using var stream = File.OpenRead(filePath);
        var rows = JsonSerializer.Deserialize<List<Row>>(stream, Options) ?? new();

        return rows.ConvertAll(row => new TransactionRecord(
            SourceId: sourceId,
            Date: DateOnly.ParseExact(row.Date, "yyyy-MM-dd"),
            AmountAgorot: Money.ToAgorot(row.Amount),
            MerchantRaw: row.MerchantRaw.Trim(),
            NativeId: string.IsNullOrWhiteSpace(row.NativeId) ? null : row.NativeId,
            Installments: row.Installments is { } i ? new InstallmentInfo(i.Number, i.Total) : null));
    }

    private sealed record Row(
        [property: JsonPropertyName("date")] string Date,
        [property: JsonPropertyName("amount")] decimal Amount,
        [property: JsonPropertyName("merchantRaw")] string MerchantRaw,
        [property: JsonPropertyName("nativeId")] string? NativeId = null,
        [property: JsonPropertyName("installments")] InstallmentRow? Installments = null);

    private sealed record InstallmentRow(
        [property: JsonPropertyName("number")] int Number,
        [property: JsonPropertyName("total")] int Total);
}

/// <summary>
/// Placeholder for the one remaining unhandled format: legacy binary <c>.xls</c>.
/// <c>.pdf</c>/<c>.xlsx</c>/<c>.xlsm</c> now have real parsers
/// (<see cref="UtilityBillPdfParser"/>, <see cref="XlsxStatementParser"/>) registered
/// ahead of this one. It claims <c>.xls</c> so an accidental drop fails loudly
/// instead of being silently skipped.
/// </summary>
public sealed class NotImplementedRawFeedParser : IRawFeedParser
{
    private static readonly string[] Extensions = { ".xls" };

    public bool CanParse(string fileName) =>
        Extensions.Any(ext => fileName.EndsWith(ext, StringComparison.OrdinalIgnoreCase));

    public IReadOnlyList<TransactionRecord> Parse(string filePath, string sourceId) =>
        throw new NotSupportedException(
            $"No parser for '{Path.GetFileName(filePath)}' yet. Legacy binary .xls isn't " +
            "handled — re-save as .xlsx if possible, or add a parser and register it ahead " +
            "of this one.");
}
