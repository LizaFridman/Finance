using ClosedXML.Excel;

namespace Finance.Core.Ingestion;

/// <summary>
/// Reads a Cal/Max historical statement workbook (<c>כאל 2025.xlsm</c>,
/// <c>Max 2025.xlsx</c> — spec §5) into <see cref="TransactionRecord"/>s. One row
/// per transaction, detected by matching row 1's (trimmed) headers against a
/// small set of accepted Hebrew/English aliases — the real column names are
/// unconfirmed (docs/missing-data-report.md), so this is written to fail loudly
/// on a mismatch rather than guess at a layout nobody has verified yet.
/// </summary>
public sealed class XlsxStatementParser : IRawFeedParser
{
    private enum Field { Date, Amount, Merchant }

    private static readonly IReadOnlyDictionary<string, Field> HeaderAliases =
        new Dictionary<string, Field>(StringComparer.OrdinalIgnoreCase)
        {
            ["תאריך"] = Field.Date,
            ["תאריך עסקה"] = Field.Date,
            ["Date"] = Field.Date,

            ["סכום"] = Field.Amount,
            ["סכום חיוב"] = Field.Amount,
            ["סכום עסקה"] = Field.Amount,
            ["Amount"] = Field.Amount,

            ["בית עסק"] = Field.Merchant,
            ["שם בית עסק"] = Field.Merchant,
            ["תיאור"] = Field.Merchant,
            ["Merchant"] = Field.Merchant,
            ["Description"] = Field.Merchant,
        };

    public bool CanParse(string fileName) =>
        fileName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase) ||
        fileName.EndsWith(".xlsm", StringComparison.OrdinalIgnoreCase);

    public IReadOnlyList<TransactionRecord> Parse(string filePath, string sourceId)
    {
        using var workbook = new XLWorkbook(filePath);
        var sheet = workbook.Worksheet(1);
        var headerRow = sheet.Row(1);
        var lastColumn = headerRow.LastCellUsed()?.Address.ColumnNumber ?? 0;

        var columns = MapColumns(headerRow, lastColumn, filePath);

        var lastRow = sheet.LastRowUsed()?.RowNumber() ?? 1;
        var records = new List<TransactionRecord>();

        for (var rowIndex = 2; rowIndex <= lastRow; rowIndex++)
        {
            var row = sheet.Row(rowIndex);
            if (row.IsEmpty())
                continue;

            var dateCell = row.Cell(columns[Field.Date]);
            var amountCell = row.Cell(columns[Field.Amount]);
            var merchantCell = row.Cell(columns[Field.Merchant]).GetString().Trim();

            if (dateCell.IsEmpty() && amountCell.IsEmpty() && merchantCell.Length == 0)
                continue;

            var date = dateCell.GetDateTime();
            var amount = amountCell.GetValue<decimal>();

            records.Add(new TransactionRecord(
                SourceId: sourceId,
                Date: DateOnly.FromDateTime(date),
                AmountAgorot: Money.ToAgorot(amount),
                MerchantRaw: merchantCell));
        }

        return records;
    }

    private static Dictionary<Field, int> MapColumns(IXLRow headerRow, int lastColumn, string filePath)
    {
        var found = new Dictionary<Field, int>();
        var headersSeen = new List<string>();

        for (var col = 1; col <= lastColumn; col++)
        {
            var header = headerRow.Cell(col).GetString().Trim();
            if (header.Length == 0)
                continue;

            headersSeen.Add(header);
            if (HeaderAliases.TryGetValue(header, out var field) && !found.ContainsKey(field))
                found[field] = col;
        }

        var missing = Enum.GetValues<Field>().Where(f => !found.ContainsKey(f)).ToList();
        if (missing.Count > 0)
        {
            throw new InvalidOperationException(
                $"'{Path.GetFileName(filePath)}': couldn't find a header for {string.Join(", ", missing)}. " +
                $"Headers found: [{string.Join(", ", headersSeen)}]. " +
                $"Accepted aliases: [{string.Join(", ", HeaderAliases.Keys)}]. " +
                "The real column names are unconfirmed for this workbook — add the actual " +
                "header(s) to XlsxStatementParser.HeaderAliases.");
        }

        return found;
    }
}
