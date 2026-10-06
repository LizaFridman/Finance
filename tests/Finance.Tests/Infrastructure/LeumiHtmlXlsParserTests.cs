using System;
using System.IO;
using Finance.Infrastructure.Ingestion;

namespace Finance.Tests.Infrastructure;

public class LeumiHtmlXlsParserTests : IDisposable
{
    // Fabricated data shaped like Bank Leumi's "תנועות בחשבון" HTML export
    // (which ships with a .xls extension despite being HTML). Names, amounts
    // and account details below are invented, not excerpts of a real statement.
    private const string SampleHtml = """
    <html>
    <head><meta charset="UTF-8"></head>
    <body>
    <table>
    <tr><td class="xlPageTitle">בנק לאומי</td></tr>
    <tr><td class="xlHeader"><span>תאריך</span></td><td class="xlHeader"><span>תאריך ערך</span></td><td class="xlHeader"><span>תיאור</span></td><td class="xlHeader"><span>אסמכתא</span></td><td class="xlHeader"><span>בחובה</span></td><td class="xlHeader"><span>בזכות</span></td><td class="xlHeader"><span>היתרה בש"ח</span></td></tr>
    <tr><td>01/03/2026</td><td>01/03/2026</td><td>סופרמרקט לדוגמה</td><td>1234</td><td>150.50</td><td>0.00</td><td>900.00</td></tr>
    <tr><td>02/03/2026</td><td>02/03/2026</td><td>זיכוי לדוגמה</td><td>1234</td><td>0.00</td><td>50.00</td><td>950.00</td></tr>
    <tr><td>not-a-date</td><td>03/03/2026</td><td>שורה פגומה</td><td>1234</td><td>10.00</td><td>0.00</td><td>940.00</td></tr>
    <tr><td>סך הכל</td></tr>
    <tr><td>1,000.00</td></tr>
    </table>
    </body>
    </html>
    """;

    private readonly string _path = Path.Combine(Path.GetTempPath(), $"leumi-{Guid.NewGuid():N}.xls");

    public LeumiHtmlXlsParserTests() => File.WriteAllText(_path, SampleHtml);

    public void Dispose() => File.Delete(_path);

    [Fact]
    public void Claims_xls_files_only()
    {
        var parser = new LeumiHtmlXlsParser();

        Assert.True(parser.CanParse("statement.xls"));
        Assert.False(parser.CanParse("statement.json"));
    }

    [Fact]
    public void A_debit_row_becomes_a_negative_outflow()
    {
        var result = new LeumiHtmlXlsParser().Parse(_path, "leumi");

        var row = Assert.Single(result.Records, r => r.MerchantRaw == "סופרמרקט לדוגמה");
        Assert.Equal(-150.50m, row.Amount.Shekels);
        Assert.Equal(new DateOnly(2026, 3, 1), row.Date);
    }

    [Fact]
    public void A_credit_row_becomes_a_positive_inflow()
    {
        var result = new LeumiHtmlXlsParser().Parse(_path, "leumi");

        var row = Assert.Single(result.Records, r => r.MerchantRaw == "זיכוי לדוגמה");
        Assert.Equal(50.00m, row.Amount.Shekels);
    }

    [Fact]
    public void A_row_with_an_unparseable_date_is_reported_but_does_not_abort_the_file()
    {
        var result = new LeumiHtmlXlsParser().Parse(_path, "leumi");

        Assert.Equal(2, result.Records.Count);
        Assert.Contains(result.Errors, e => e.Contains("row 3"));
    }

    [Fact]
    public void The_totals_row_is_not_ingested_as_a_transaction()
    {
        var result = new LeumiHtmlXlsParser().Parse(_path, "leumi");

        Assert.DoesNotContain(result.Records, r => r.MerchantRaw.Contains("סך הכל"));
    }
}
