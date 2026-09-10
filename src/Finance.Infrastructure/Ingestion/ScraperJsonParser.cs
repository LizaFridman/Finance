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
        var rows = JsonSerializer.Deserialize<List<Row>>(stream, Options) ?? [];

        return rows.ConvertAll(row => new TransactionRecord(
            SourceId: sourceId,
            Date: DateOnly.ParseExact(row.Date, "yyyy-MM-dd", CultureInfo.InvariantCulture),
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
