using System.Globalization;
using Finance.Domain;
using Finance.Domain.Ingestion;
using HtmlAgilityPack;

namespace Finance.Infrastructure.Ingestion;

/// <summary>
/// Reads Bank Leumi's "תנועות בחשבון" export. Despite the <c>.xls</c>
/// extension it is an HTML table (Leumi's web exporter), not binary Excel —
/// this parser finds the transactions table by header text rather than
/// fixed row/column indices, since the surrounding page markup (balance
/// summary, styling) is not part of the contract.
/// </summary>
public sealed class LeumiHtmlXlsParser : IRawFeedParser
{
    private const string DateHeader = "תאריך";
    private const string DescriptionHeader = "תיאור";
    private const string DebitHeader = "בחובה";
    private const string CreditHeader = "בזכות";
    private const string TotalRowMarker = "סך הכל";

    public bool CanParse(string fileName) =>
        fileName.EndsWith(".xls", StringComparison.OrdinalIgnoreCase);

    public RawFeedParseResult Parse(string filePath, string sourceId)
    {
        var fileName = Path.GetFileName(filePath);
        var html = File.ReadAllText(filePath);

        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        var rows = doc.DocumentNode.SelectNodes("//tr");
        if (rows is null)
            return new RawFeedParseResult([], [$"{fileName}: no <tr> rows found"]);

        var headerRowIndex = -1;
        List<string>? headers = null;
        for (var i = 0; i < rows.Count; i++)
        {
            var cells = DirectCells(rows[i]);
            if (cells.Any(c => c == DateHeader))
            {
                headerRowIndex = i;
                headers = cells;
                break;
            }
        }

        if (headers is null)
            return new RawFeedParseResult([], [$"{fileName}: transactions header row not found"]);

        int dateCol = headers.IndexOf(DateHeader);
        int descriptionCol = headers.IndexOf(DescriptionHeader);
        int debitCol = headers.IndexOf(DebitHeader);
        int creditCol = headers.IndexOf(CreditHeader);

        if (dateCol < 0 || descriptionCol < 0 || debitCol < 0 || creditCol < 0)
            return new RawFeedParseResult([], [$"{fileName}: expected column not found in header row"]);

        var records = new List<TransactionRecord>();
        var errors = new List<string>();
        var dataRowNumber = 0;

        for (var i = headerRowIndex + 1; i < rows.Count; i++)
        {
            var cells = DirectCells(rows[i]);
            if (cells.Count == 0)
                continue;
            if (cells.Any(c => c.Contains(TotalRowMarker, StringComparison.Ordinal)))
                break;

            dataRowNumber++;
            try
            {
                records.Add(ToRecord(cells, dateCol, descriptionCol, debitCol, creditCol, sourceId));
            }
            catch (Exception ex) when (ex is FormatException or ArgumentException or IndexOutOfRangeException)
            {
                errors.Add($"{fileName} row {dataRowNumber}: {ex.Message}");
            }
        }

        return new RawFeedParseResult(records, errors);
    }

    private static TransactionRecord ToRecord(
        IReadOnlyList<string> cells, int dateCol, int descriptionCol, int debitCol, int creditCol, string sourceId)
    {
        if (dateCol >= cells.Count || descriptionCol >= cells.Count || debitCol >= cells.Count || creditCol >= cells.Count)
            throw new FormatException("row has fewer columns than the header");

        var dateText = cells[dateCol];
        if (!DateOnly.TryParseExact(dateText, "dd/MM/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
            throw new FormatException($"unparseable date '{dateText}'");

        var description = cells[descriptionCol];
        if (string.IsNullOrWhiteSpace(description))
            throw new FormatException("missing description");

        var debit = ParseAmount(cells[debitCol]);
        var credit = ParseAmount(cells[creditCol]);

        if (debit > 0)
            return new TransactionRecord(sourceId, date, Money.FromShekels(-debit), description.Trim());
        if (credit > 0)
            return new TransactionRecord(sourceId, date, Money.FromShekels(credit), description.Trim());

        throw new FormatException("row has neither a debit nor a credit amount");
    }

    private static decimal ParseAmount(string text) =>
        string.IsNullOrWhiteSpace(text)
            ? 0m
            : decimal.Parse(text, NumberStyles.AllowThousands | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture);

    private static List<string> DirectCells(HtmlNode row) =>
        row.ChildNodes
            .Where(n => n.Name == "td")
            .Select(n => HtmlEntity.DeEntitize(n.InnerText).Trim())
            .ToList();
}
