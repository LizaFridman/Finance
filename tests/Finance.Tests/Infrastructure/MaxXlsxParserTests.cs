using System;
using System.IO;
using ClosedXML.Excel;
using Finance.Infrastructure.Ingestion;

namespace Finance.Tests.Infrastructure;

public class MaxXlsxParserTests : IDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"max-{Guid.NewGuid():N}.xlsx");

    // Builds a workbook shaped like Max's monthly export: three metadata rows
    // (holder name, card id, month) above the header row, then data rows.
    // All names/amounts below are invented, not excerpts of a real statement.
    public MaxXlsxParserTests()
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Sheet1");

        sheet.Cell(1, 1).Value = "שם דוגמה-000000000";
        sheet.Cell(2, 1).Value = "0000-max example";
        sheet.Cell(3, 1).Value = "01/2026";

        sheet.Cell(4, 1).Value = "תאריך עסקה";
        sheet.Cell(4, 2).Value = "שם בית העסק";
        sheet.Cell(4, 3).Value = "סכום חיוב";

        sheet.Cell(5, 1).Value = "03-01-2026";
        sheet.Cell(5, 2).Value = "חנות לדוגמה";
        sheet.Cell(5, 3).Value = 100.00m;

        sheet.Cell(6, 1).Value = "04-01-2026";
        sheet.Cell(6, 2).Value = "החזר לדוגמה";
        sheet.Cell(6, 3).Value = -20.00m;

        sheet.Cell(7, 1).Value = "not-a-date";
        sheet.Cell(7, 2).Value = "שורה פגומה";
        sheet.Cell(7, 3).Value = 10.00m;

        sheet.Cell(8, 1).Value = "סך הכל";
        sheet.Cell(9, 1).Value = "80.00₪";

        workbook.SaveAs(_path);
    }

    public void Dispose() => File.Delete(_path);

    [Fact]
    public void Claims_xlsx_files_only()
    {
        var parser = new MaxXlsxParser();

        Assert.True(parser.CanParse("export.xlsx"));
        Assert.False(parser.CanParse("export.numbers"));
        Assert.False(parser.CanParse("export.xlsm"));
    }

    [Fact]
    public void A_positive_charge_becomes_a_negative_outflow()
    {
        var result = new MaxXlsxParser().Parse(_path, "max");

        var row = Assert.Single(result.Records, r => r.MerchantRaw == "חנות לדוגמה");
        Assert.Equal(-100.00m, row.Amount.Shekels);
        Assert.Equal(new DateOnly(2026, 1, 3), row.Date);
    }

    [Fact]
    public void A_negative_charge_refund_becomes_a_positive_inflow()
    {
        var result = new MaxXlsxParser().Parse(_path, "max");

        var row = Assert.Single(result.Records, r => r.MerchantRaw == "החזר לדוגמה");
        Assert.Equal(20.00m, row.Amount.Shekels);
    }

    [Fact]
    public void A_row_with_an_unparseable_date_is_reported_but_does_not_abort_the_file()
    {
        var result = new MaxXlsxParser().Parse(_path, "max");

        Assert.Equal(2, result.Records.Count);
        Assert.Contains(result.Errors, e => e.Contains("row 3"));
    }

    [Fact]
    public void The_totals_footer_row_is_not_ingested_as_a_transaction()
    {
        var result = new MaxXlsxParser().Parse(_path, "max");

        Assert.DoesNotContain(result.Records, r => r.MerchantRaw.Contains("סך הכל"));
    }
}
