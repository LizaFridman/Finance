using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Finance.Domain;
using Finance.Domain.Ingestion;

namespace Finance.Infrastructure.Ingestion;

/// <summary>
/// Reads the normalized JSON array emitted by the Node scraper wrapper
/// (<c>scraper/</c>). This is the only feed format implemented this session;
/// PDF/xlsx parsing is deferred — see <see cref="NotImplementedRawFeedParser"/>.
/// A row that can't be read (bad date, missing merchant, …) is reported, not
/// fatal — the rest of the file still ingests.
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

    public RawFeedParseResult Parse(string filePath, string sourceId)
    {
        using var stream = File.OpenRead(filePath);
        var rows = JsonSerializer.Deserialize<List<Row>>(stream, Options) ?? [];

        var fileName = Path.GetFileName(filePath);
        var records = new List<TransactionRecord>(rows.Count);
        var errors = new List<string>();

        for (var i = 0; i < rows.Count; i++)
        {
            try
            {
                records.Add(ToRecord(rows[i], sourceId));
            }
            catch (Exception ex) when (ex is FormatException or ArgumentException)
            {
                errors.Add($"{fileName} row {i + 1}: {ex.Message}");
            }
        }

        return new RawFeedParseResult(records, errors);
    }

    private static TransactionRecord ToRecord(Row row, string sourceId)
    {
        if (string.IsNullOrWhiteSpace(row.Date))
            throw new FormatException("missing 'date'");
        if (string.IsNullOrWhiteSpace(row.MerchantRaw))
            throw new FormatException("missing 'merchantRaw'");

        return new TransactionRecord(
            SourceId: sourceId,
            Date: DateOnly.ParseExact(row.Date, "yyyy-MM-dd", CultureInfo.InvariantCulture),
            Amount: Money.FromShekels(row.Amount),
            MerchantRaw: row.MerchantRaw.Trim(),
            NativeId: string.IsNullOrWhiteSpace(row.NativeId) ? null : row.NativeId,
            Installments: row.Installments is { } inst ? new InstallmentInfo(inst.Number, inst.Total) : null);
    }

    private sealed record Row(
        [property: JsonPropertyName("date")] string? Date,
        [property: JsonPropertyName("amount")] decimal Amount,
        [property: JsonPropertyName("merchantRaw")] string? MerchantRaw,
        [property: JsonPropertyName("nativeId")] string? NativeId = null,
        [property: JsonPropertyName("installments")] InstallmentRow? Installments = null);

    private sealed record InstallmentRow(
        [property: JsonPropertyName("number")] int Number,
        [property: JsonPropertyName("total")] int Total);
}
