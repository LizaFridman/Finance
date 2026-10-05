using ClosedXML.Excel;
using Finance.Core.Ingestion;

namespace Finance.Tests.Core;

public class XlsxStatementParserTests : IDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"stmt-{Guid.NewGuid():N}.xlsx");

    public void Dispose() => File.Delete(_path);

    private void WriteWorkbook(string[] headers, object?[][] rows)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.AddWorksheet("Statement");

        for (var col = 0; col < headers.Length; col++)
            sheet.Cell(1, col + 1).Value = headers[col];

        for (var r = 0; r < rows.Length; r++)
            for (var col = 0; col < rows[r].Length; col++)
                sheet.Cell(r + 2, col + 1).Value = XLCellValue.FromObject(rows[r][col]);

        workbook.SaveAs(_path);
    }

    [Fact]
    public void Hebrew_headers_are_recognized()
    {
        WriteWorkbook(
            headers: new[] { "תאריך", "סכום", "בית עסק" },
            rows: new object?[][]
            {
                new object?[] { new DateTime(2025, 3, 4), -123.45m, "שופרסל אונליין" },
            });

        var records = new XlsxStatementParser().Parse(_path, "cal");

        Assert.Equal(1, records.Count);
        Assert.Equal(new DateOnly(2025, 3, 4), records[0].Date);
        Assert.Equal(-12345, records[0].AmountAgorot);
        Assert.Equal("שופרסל אונליין", records[0].MerchantRaw);
        Assert.Equal("cal", records[0].SourceId);
    }

    [Fact]
    public void English_headers_are_recognized()
    {
        WriteWorkbook(
            headers: new[] { "Date", "Amount", "Description" },
            rows: new object?[][]
            {
                new object?[] { new DateTime(2025, 6, 1), -50.00m, "Wolt" },
            });

        var records = new XlsxStatementParser().Parse(_path, "max");

        Assert.Equal(1, records.Count);
        Assert.Equal("Wolt", records[0].MerchantRaw);
    }

    [Fact]
    public void A_missing_required_column_throws_naming_what_was_found()
    {
        WriteWorkbook(
            headers: new[] { "תאריך", "בית עסק" }, // no amount column
            rows: new object?[][] { new object?[] { new DateTime(2025, 1, 1), "x" } });

        var ex = Assert.Throws<InvalidOperationException>(
            () => new XlsxStatementParser().Parse(_path, "cal"));

        Assert.Contains("Amount", ex.Message);
        Assert.Contains("תאריך", ex.Message);
    }

    [Fact]
    public void A_blank_row_is_skipped()
    {
        using (var workbook = new XLWorkbook())
        {
            var sheet = workbook.AddWorksheet("Statement");
            var headers = new[] { "תאריך", "סכום", "בית עסק" };
            for (var col = 0; col < headers.Length; col++)
                sheet.Cell(1, col + 1).Value = headers[col];

            sheet.Cell(2, 1).Value = new DateTime(2025, 3, 4);
            sheet.Cell(2, 2).Value = -10.00m;
            sheet.Cell(2, 3).Value = "x";
            // row 3 deliberately left untouched -- a blank row between real ones.
            sheet.Cell(4, 1).Value = new DateTime(2025, 3, 5);
            sheet.Cell(4, 2).Value = -20.00m;
            sheet.Cell(4, 3).Value = "y";

            workbook.SaveAs(_path);
        }

        var records = new XlsxStatementParser().Parse(_path, "cal");

        Assert.Equal(2, records.Count);
    }
}
