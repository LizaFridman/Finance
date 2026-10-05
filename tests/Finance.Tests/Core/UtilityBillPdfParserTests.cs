using Finance.Core.Ingestion;

namespace Finance.Tests.Core;

public class UtilityBillPdfParserTests
{
    [Fact]
    public void A_hebrew_bill_with_total_and_due_date_parses()
    {
        var text = """
        תאגיד המים והביוב
        חשבון מים לתקופה 01/07/2025 - 31/08/2025
        תאריך לתשלום: 15/09/2025
        סה"כ לתשלום: 342.80
        """;

        var record = CallParseBillText(text, "water", "water_2025_08.pdf");

        Assert.Equal("water", record.SourceId);
        Assert.Equal(new DateOnly(2025, 9, 15), record.Date);
        Assert.Equal(-34280, record.AmountAgorot); // a bill is always an outflow
        Assert.Equal("water_2025_08", record.NativeId);
    }

    [Fact]
    public void An_english_style_bill_parses()
    {
        var text = """
        Partner Communications
        Invoice period: 01/03/2025 - 31/03/2025
        Due Date: 10/04/2025
        Total Due: NIS 129.90
        """;

        var record = CallParseBillText(text, "partner", "partner_march.pdf");

        Assert.Equal(new DateOnly(2025, 4, 10), record.Date);
        Assert.Equal(-12990, record.AmountAgorot);
    }

    [Fact]
    public void A_missing_amount_throws_naming_the_file()
    {
        var text = "חשבון חשמל\nתאריך לתשלום: 15/09/2025";

        var ex = Assert.Throws<InvalidOperationException>(
            () => CallParseBillText(text, "electricity", "elec.pdf"));

        Assert.Contains("elec.pdf", ex.Message);
        Assert.Contains("amount", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void A_missing_date_throws_naming_the_file()
    {
        var text = "וועד בית\nסה\"כ לתשלום: 100.00";

        var ex = Assert.Throws<InvalidOperationException>(
            () => CallParseBillText(text, "vaad", "vaad.pdf"));

        Assert.Contains("vaad.pdf", ex.Message);
    }

    private static TransactionRecord CallParseBillText(string text, string sourceId, string fileName) =>
        UtilityBillPdfParser.ParseBillText(text, sourceId, fileName);
}
