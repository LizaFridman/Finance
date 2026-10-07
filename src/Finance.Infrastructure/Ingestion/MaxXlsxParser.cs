using System.Globalization;
using ClosedXML.Excel;
using Finance.Domain;
using Finance.Domain.Ingestion;

namespace Finance.Infrastructure.Ingestion;

/// <summary>
/// Reads Max's monthly credit-card statement export (genuine OOXML, unlike
/// Leumi's HTML-as-.xls). The header row is found by text rather than a
/// fixed row index, since a few metadata rows (holder name, card id, month)
/// sit above it. Charge amounts in the file are positive; a refund is a
/// negative charge amount — negating uniformly maps both onto the domain's
/// "negative = outflow" convention.
/// </summary>
public sealed class MaxXlsxParser : IRawFeedParser
{
    private const string DateHeader = "תאריך עסקה";
    private const string MerchantHeader = "שם בית העסק";
    private const string ChargedAmountHeader = "סכום חיוב";
    private const string TotalRowMarker = "סך הכל";

    public bool CanParse(string fileName) =>
        fileName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase);

    public RawFeedParseResult Parse(string filePath, string sourceId)
    {
        var fileName = Path.GetFileName(filePath);
        using var workbook = new XLWorkbook(filePath);
        var sheet = workbook.Worksheets.First();

        var headerRow = sheet.RowsUsed()
            .FirstOrDefault(r => r.Cells().Any(c => c.GetString().Trim() == DateHeader));
        if (headerRow is null)
            return new RawFeedParseResult([], [$"{fileName}: transactions header row not found"]);

        int dateCol = -1, merchantCol = -1, amountCol = -1;
        foreach (var cell in headerRow.CellsUsed())
        {
            var text = cell.GetString().Trim();
            if (text == DateHeader) dateCol = cell.Address.ColumnNumber;
            else if (text == MerchantHeader) merchantCol = cell.Address.ColumnNumber;
            else if (text == ChargedAmountHeader) amountCol = cell.Address.ColumnNumber;
        }

        if (dateCol < 0 || merchantCol < 0 || amountCol < 0)
            return new RawFeedParseResult([], [$"{fileName}: expected column not found in header row"]);

        var records = new List<TransactionRecord>();
        var errors = new List<string>();
        var dataRowNumber = 0;

        var lastRow = sheet.LastRowUsed()?.RowNumber() ?? headerRow.RowNumber();
        for (var rowNum = headerRow.RowNumber() + 1; rowNum <= lastRow; rowNum++)
        {
            var row = sheet.Row(rowNum);
            var firstCellText = row.Cell(dateCol).GetString().Trim();
            if (string.IsNullOrWhiteSpace(firstCellText))
                continue;
            if (firstCellText.Contains(TotalRowMarker, StringComparison.Ordinal))
                break;

            dataRowNumber++;
            try
            {
                records.Add(ToRecord(row, dateCol, merchantCol, amountCol, sourceId));
            }
            catch (Exception ex) when (ex is FormatException or ArgumentException)
            {
                errors.Add($"{fileName} row {dataRowNumber}: {ex.Message}");
            }
        }

        return new RawFeedParseResult(records, errors);
    }

    private static TransactionRecord ToRecord(IXLRow row, int dateCol, int merchantCol, int amountCol, string sourceId)
    {
        var dateText = row.Cell(dateCol).GetString().Trim();
        if (!DateOnly.TryParseExact(dateText, "dd-MM-yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
            throw new FormatException($"unparseable date '{dateText}'");

        var merchant = row.Cell(merchantCol).GetString().Trim();
        if (string.IsNullOrWhiteSpace(merchant))
            throw new FormatException("missing merchant");

        var amountCell = row.Cell(amountCol);
        if (!amountCell.TryGetValue<decimal>(out var chargedAmount))
            throw new FormatException($"unparseable amount '{amountCell.GetString()}'");

        return new TransactionRecord(sourceId, date, Money.FromShekels(-chargedAmount), merchant);
    }
}
