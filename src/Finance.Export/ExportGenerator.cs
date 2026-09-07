using System.Globalization;
using System.Text;
using ClosedXML.Excel;

namespace Finance.Export;

/// <summary>One row shaped for the old Daily Expenses columns (spec §5).</summary>
public sealed record LegacyExportRow(
    DateTime Timestamp,
    DateOnly Date,
    decimal Amount,
    string Category,
    string SplitMethod,
    string PaymentMethod,
    bool Recurring,
    string Note);

/// <summary>
/// Generates a local file on demand (spec §9). This is output only: the template
/// is opened read-only to learn its column order, and no database connection is
/// ever involved. The generated file is never read back into the system.
/// </summary>
public sealed class ExportGenerator
{
    /// <summary>
    /// Canonical (trimmed) legacy header -> how to pull that value from a row.
    /// A template column whose trimmed header isn't here is written blank rather
    /// than guessed at.
    /// </summary>
    private static readonly IReadOnlyDictionary<string, Func<LegacyExportRow, object?>> FieldByHeader =
        new Dictionary<string, Func<LegacyExportRow, object?>>(StringComparer.OrdinalIgnoreCase)
        {
            ["Timestamp"] = r => r.Timestamp,
            ["Date"] = r => r.Date.ToString("yyyy-MM-dd"),
            ["Amount"] = r => r.Amount,
            ["Category"] = r => r.Category,
            ["Split Method"] = r => r.SplitMethod,
            ["Payment Method"] = r => r.PaymentMethod,
            ["Recurring?"] = r => r.Recurring ? "Yes" : "No",
            ["Note"] = r => r.Note,
        };

    /// <summary>
    /// Writes <paramref name="outputPath"/> as a fresh xlsx whose columns match,
    /// in order and by (trimmed) name, the first row of <paramref name="templatePath"/>.
    /// </summary>
    public void WriteLegacyXlsx(
        string templatePath, IEnumerable<LegacyExportRow> rows, string outputPath)
    {
        var headers = ReadTrimmedHeaders(templatePath);

        using var workbook = new XLWorkbook();
        var sheet = workbook.AddWorksheet("Export");

        for (var col = 0; col < headers.Count; col++)
            sheet.Cell(1, col + 1).Value = headers[col];

        var rowIndex = 2;
        foreach (var row in rows)
        {
            for (var col = 0; col < headers.Count; col++)
            {
                if (!FieldByHeader.TryGetValue(headers[col], out var selector))
                    continue;
                sheet.Cell(rowIndex, col + 1).Value = XLCellValue.FromObject(selector(row));
            }
            rowIndex++;
        }

        workbook.SaveAs(outputPath);
    }

    public void WriteCsv(
        IEnumerable<string> headers, IEnumerable<LegacyExportRow> rows, string outputPath)
    {
        var headerList = headers.Select(h => h.Trim()).ToList();
        var builder = new StringBuilder();
        builder.AppendLine(string.Join(',', headerList.Select(Escape)));

        foreach (var row in rows)
        {
            var cells = headerList.Select(h =>
                FieldByHeader.TryGetValue(h, out var selector)
                    ? Escape(Stringify(selector(row)))
                    : string.Empty);
            builder.AppendLine(string.Join(',', cells));
        }

        File.WriteAllText(outputPath, builder.ToString());
    }

    private static IReadOnlyList<string> ReadTrimmedHeaders(string templatePath)
    {
        // Read-only: the template is a translation reference, never a write target (spec §9).
        using var workbook = new XLWorkbook(templatePath);
        var sheet = workbook.Worksheet(1);
        var headerRow = sheet.Row(1);
        var lastColumn = headerRow.LastCellUsed()?.Address.ColumnNumber ?? 0;

        var headers = new List<string>(lastColumn);
        for (var col = 1; col <= lastColumn; col++)
            headers.Add(headerRow.Cell(col).GetString().Trim());
        return headers;
    }

    private static string Stringify(object? value) => value switch
    {
        null => string.Empty,
        decimal d => d.ToString(CultureInfo.InvariantCulture),
        DateTime dt => dt.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
        IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
        _ => value.ToString() ?? string.Empty,
    };

    private static string Escape(string value) =>
        value.Contains(',') || value.Contains('"') || value.Contains('\n')
            ? $"\"{value.Replace("\"", "\"\"")}\""
            : value;
}
