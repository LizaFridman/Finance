using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using ClosedXML.Excel;
using Finance.Export;

namespace Finance.Tests.Export;

public class ExportGeneratorTests : IDisposable
{
    private readonly string _dir;

    public ExportGeneratorTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), $"export-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_dir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch (IOException) { }
    }

    /// <summary>A stand-in for the old Daily Expenses sheet, headers padded exactly as spec §5 notes.</summary>
    private string MakeTemplate()
    {
        var path = Path.Combine(_dir, "daily-expenses-template.xlsx");
        using var wb = new XLWorkbook();
        var ws = wb.AddWorksheet("Sheet1");
        var headers = new[]
        {
            "Timestamp", " Date", "Amount", "  Category  ", "Split Method ",
            "Payment Method", "Recurring? ", " Note",
        };
        for (var i = 0; i < headers.Length; i++)
            ws.Cell(1, i + 1).Value = headers[i];
        wb.SaveAs(path);
        return path;
    }

    private static LegacyExportRow SampleRow(string splitMethod) => new(
        Timestamp: new DateTime(2025, 2, 3, 9, 30, 0),
        Date: new DateOnly(2025, 2, 3),
        Amount: -123.45m,
        Category: "Groceries",
        SplitMethod: splitMethod,
        PaymentMethod: "Leumi",
        Recurring: false,
        Note: "");

    [Fact]
    public void Output_headers_are_read_live_from_the_template_and_trimmed()
    {
        var template = MakeTemplate();
        var output = Path.Combine(_dir, "out.xlsx");

        new ExportGenerator().WriteLegacyXlsx(template, new[] { SampleRow("50/50") }, output);

        using var wb = new XLWorkbook(output);
        var ws = wb.Worksheet(1);
        Assert.Equal(
            new[] { "Timestamp", "Date", "Amount", "Category", "Split Method",
                    "Payment Method", "Recurring?", "Note" },
            Enumerable.Range(1, 8).Select(c => ws.Cell(1, c).GetString()));
    }

    [Fact]
    public void Values_land_under_the_matching_trimmed_header()
    {
        var template = MakeTemplate();
        var output = Path.Combine(_dir, "out.xlsx");

        new ExportGenerator().WriteLegacyXlsx(template, new[] { SampleRow("50/50") }, output);

        using var wb = new XLWorkbook(output);
        var ws = wb.Worksheet(1);
        Assert.Equal("Groceries", ws.Cell(2, 4).GetString());      // the "  Category  " column
        Assert.Equal("50/50", ws.Cell(2, 5).GetString());          // the "Split Method " column
    }

    [Fact]
    public void The_legacy_Partner_100_percent_split_value_round_trips()
    {
        var template = MakeTemplate();
        var output = Path.Combine(_dir, "out.xlsx");
        var split = LegacySplitMethodMap.ForBucket("personal_akumu");

        new ExportGenerator().WriteLegacyXlsx(template, new[] { SampleRow(split) }, output);

        using var wb = new XLWorkbook(output);
        Assert.Equal("Partner 100%", wb.Worksheet(1).Cell(2, 5).GetString());
    }

    [Fact]
    public void Exporting_never_modifies_the_template_file()
    {
        var template = MakeTemplate();
        var before = Sha256(template);

        new ExportGenerator().WriteLegacyXlsx(
            template, new[] { SampleRow("50/50") }, Path.Combine(_dir, "out.xlsx"));

        Assert.Equal(before, Sha256(template));
    }

    [Fact]
    public void Csv_export_writes_the_given_headers_and_rows()
    {
        var output = Path.Combine(_dir, "out.csv");

        new ExportGenerator().WriteCsv(
            new[] { "Date", "Amount", "Category", "Split Method" },
            new[] { SampleRow("Partner 100%") },
            output);

        var lines = File.ReadAllLines(output);
        Assert.Equal("Date,Amount,Category,Split Method", lines[0]);
        Assert.Equal("2025-02-03,-123.45,Groceries,Partner 100%", lines[1]);
    }

    private static string Sha256(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream));
    }
}
