using Finance.Domain.Ingestion;
using Finance.Infrastructure.Ingestion;

namespace Finance.Tests.Infrastructure;

public class UtilityBillPdfParserTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), $"bill-{Guid.NewGuid():N}");

    public UtilityBillPdfParserTests() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch (IOException) { }
    }

    [Fact]
    public void A_hebrew_bill_with_total_and_due_date_parses()
    {
        var text = """
        תאגיד המים והביוב
        חשבון מים לתקופה 01/07/2025 - 31/08/2025
        תאריך לתשלום: 15/09/2025
        סה"כ לתשלום: 342.80
        """;

        var result = UtilityBillPdfParser.ParseBillText(text, "water", "water_2025_08.pdf");

        Assert.Empty(result.Errors);
        var record = Assert.Single(result.Records);
        Assert.Equal("water", record.SourceId);
        Assert.Equal(new DateOnly(2025, 9, 15), record.Date);
        Assert.Equal(-34280, record.Amount.Agorot); // a bill is always an outflow
        Assert.Equal("water_2025_08", record.NativeId);
    }

    [Fact]
    public void An_english_style_bill_parses_using_the_due_date_not_the_period_start()
    {
        var text = """
        Partner Communications
        Invoice period: 01/03/2025 - 31/03/2025
        Due Date: 10/04/2025
        Total Due: NIS 1,129.90
        """;

        var result = UtilityBillPdfParser.ParseBillText(text, "partner", "partner_march.pdf");

        var record = Assert.Single(result.Records);
        Assert.Equal(new DateOnly(2025, 4, 10), record.Date);
        Assert.Equal(-112990, record.Amount.Agorot);
    }

    [Fact]
    public void A_missing_amount_is_reported_not_guessed()
    {
        var result = UtilityBillPdfParser.ParseBillText(
            "חשבון חשמל\nתאריך לתשלום: 15/09/2025", "electricity", "elec.pdf");

        Assert.Empty(result.Records);
        Assert.Contains(result.Errors, e => e.Contains("elec.pdf") && e.Contains("amount"));
    }

    [Fact]
    public void An_unlabeled_date_is_not_picked_up_from_the_period_range()
    {
        var text = """
        וועד בית
        לתקופה 01/07/2025 - 31/08/2025
        סה"כ לתשלום: 100.00
        """;

        var result = UtilityBillPdfParser.ParseBillText(text, "vaad", "vaad.pdf");

        Assert.Empty(result.Records);
        Assert.Contains(result.Errors, e => e.Contains("vaad.pdf") && e.Contains("date"));
    }

    [Fact]
    public void An_impossible_date_is_reported_not_thrown()
    {
        var result = UtilityBillPdfParser.ParseBillText(
            "תאריך לתשלום: 45/13/2025\nסה\"כ לתשלום: 10.00", "gas", "gas.pdf");

        Assert.Empty(result.Records);
        Assert.Contains(result.Errors, e => e.Contains("couldn't parse"));
    }

    [Fact]
    public void A_corrupt_pdf_is_reported_not_thrown()
    {
        var path = Path.Combine(_dir, "broken.pdf");
        File.WriteAllText(path, "%PDF-1.4 not a real pdf");

        var result = new UtilityBillPdfParser().Parse(path, "water");

        Assert.Empty(result.Records);
        // Whether PdfPig throws or yields empty text, the file is reported by name.
        Assert.Contains(result.Errors, e => e.Contains("broken.pdf"));
    }

    [Fact]
    public void Only_pdf_files_are_claimed()
    {
        var parser = new UtilityBillPdfParser();

        Assert.True(parser.CanParse("bill.PDF"));
        Assert.False(parser.CanParse("statement.xlsx"));
        Assert.False(parser.CanParse("statement.xlsm"));
    }
}
