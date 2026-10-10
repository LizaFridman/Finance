using Finance.Domain.Ingestion;
using Finance.Infrastructure.Ingestion;

namespace Finance.Tests.Infrastructure;

/// <summary>
/// Synthetic statements only: glyphs are laid out with the geometry the parser reads
/// (right-aligned columns, Hebrew in visual order) but every merchant, amount and date
/// is invented. No real statement content belongs in this repository.
/// </summary>
public class CalStatementPdfParserTests
{
    private const double GlyphWidth = 3.0;
    private const double SpaceAdvance = 2.3;
    private const double DateX = 523.1;
    private const double MerchantRight = 519.6;
    private const double CategoryRight = 436.2;
    private const double DetailsRight = 400.0;

    // ---- layout helpers -------------------------------------------------------------

    private static List<PositionedGlyph> Ltr(string text, double x0, double y)
    {
        var glyphs = new List<PositionedGlyph>();
        var x = x0;
        foreach (var c in text)
        {
            if (c == ' ') { x += SpaceAdvance; continue; }
            glyphs.Add(new PositionedGlyph(c.ToString(), x, x + GlyphWidth, y));
            x += GlyphWidth;
        }

        return glyphs;
    }

    private static double Width(string visual) =>
        visual.Sum(c => c == ' ' ? SpaceAdvance : GlyphWidth);

    /// <summary>Logical right-to-left text -> glyphs in visual order, right edge at <paramref name="right"/>.</summary>
    private static List<PositionedGlyph> Rtl(string logical, double right, double y)
    {
        var tokens = logical.Split(' ')
            .Select(t => t.Any(c => c is >= '֐' and <= '׿') ? new string(t.Reverse().ToArray()) : t)
            .Reverse();
        var visual = string.Join(' ', tokens);
        return Ltr(visual, right - Width(visual), y);
    }

    private static List<PositionedGlyph> Latin(string text, double right, double y) =>
        Ltr(text, right - Width(text), y);

    private sealed class Page
    {
        public List<PositionedGlyph> Glyphs { get; } = [];
        private double _y = 700;

        public Page Heading(string logical)
        {
            Glyphs.AddRange(Rtl(logical, 558, _y));
            _y -= 20;
            return this;
        }

        public Page Row(string date, string merchantRtl, string charge, string? original = null,
            string? details = null, bool latinMerchant = false)
        {
            Glyphs.AddRange(Ltr(date, DateX, _y));
            Glyphs.AddRange(latinMerchant ? Latin(merchantRtl, MerchantRight, _y) : Rtl(merchantRtl, MerchantRight, _y));
            Glyphs.AddRange(Rtl("קטגוריה", CategoryRight, _y));
            Glyphs.AddRange(Ltr(charge, 170, _y));
            Glyphs.AddRange(Ltr(original ?? charge, 240, _y));
            Glyphs.AddRange(Rtl("לא", 300, _y));
            if (details is not null)
                Glyphs.AddRange(Rtl(details, DetailsRight, _y));
            _y -= 9.5;
            return this;
        }

        public Page PromoRepeat(string date, string merchantRtl)
        {
            // Second printed line of a row with a promotion note: same date and merchant, no amounts.
            Glyphs.AddRange(Ltr(date, DateX, _y));
            Glyphs.AddRange(Rtl(merchantRtl, MerchantRight, _y));
            _y -= 9.5;
            return this;
        }

        public Page Total(string amount)
        {
            _y -= 5;
            Glyphs.AddRange(Ltr(amount, 159, _y));
            Glyphs.AddRange(Rtl("סה\"כ לתאריך", 300, _y));
            _y -= 20;
            return this;
        }
    }

    private static RawFeedParseResult Parse(params Page[] pages) =>
        CalStatementPdfParser.ParsePages(pages.Select(p => (IReadOnlyList<PositionedGlyph>)p.Glyphs).ToList(),
            "cal", "statement.pdf");

    private const string Accumulated = "פירוט עסקות שנצברו עד";
    private const string ChargedBefore = "פירוט עסקות אשר חויבו לפני";
    private const string Future = "פירוט עסקות לחיוב עתידי";

    // ---- tests --------------------------------------------------------------------------

    [Fact]
    public void A_plain_row_becomes_a_negative_outflow()
    {
        var page = new Page().Heading(Accumulated)
            .Row("14/03/2031", "חנות בדיקה", "₪ 61.37")
            .Total("₪ 61.37");

        var result = Parse(page);

        Assert.Empty(result.Errors);
        var record = Assert.Single(result.Records);
        Assert.Equal("cal", record.SourceId);
        Assert.Equal(new DateOnly(2031, 3, 14), record.Date);
        Assert.Equal(-6137, record.Amount.Agorot);
        Assert.Equal("חנות בדיקה", record.MerchantRaw);
        Assert.Null(record.Installments);
    }

    [Fact]
    public void A_latin_merchant_keeps_its_spelling()
    {
        var page = new Page().Heading(Accumulated)
            .Row("14/03/2031", "TEST SHOP ONLINE", "₪ 12.30", latinMerchant: true)
            .Total("₪ 12.30");

        var result = Parse(page);

        Assert.Equal("TEST SHOP ONLINE", Assert.Single(result.Records).MerchantRaw);
    }

    [Fact]
    public void A_foreign_currency_row_uses_the_shekel_charge()
    {
        var page = new Page().Heading(Accumulated)
            .Row("15/03/2031", "TEST FOREIGN", "₪ 142.88", "$ 41.77", latinMerchant: true)
            .Total("₪ 142.88");

        var result = Parse(page);

        Assert.Empty(result.Errors);
        Assert.Equal(-14288, Assert.Single(result.Records).Amount.Agorot);
    }

    [Fact]
    public void A_credit_is_an_inflow()
    {
        var page = new Page().Heading(Accumulated)
            .Row("15/03/2031", "חנות בדיקה", "₪ -233.60", details: "זיכוי")
            .Total("₪ -233.60");

        var result = Parse(page);

        Assert.Empty(result.Errors);
        Assert.Equal(23360, Assert.Single(result.Records).Amount.Agorot);
    }

    [Fact]
    public void The_first_installment_carries_the_full_amount_and_later_ones_are_marked()
    {
        var page = new Page().Heading(Accumulated)
            .Row("21/02/2031", "חנות תשלומים", "₪ 100.00", "₪ 300.00", details: "תשלום 1 מ - 3")
            .Row("03/11/2030", "חנות ישנה", "₪ 80.00", "₪ 240.00", details: "תשלום 2 מ - 3")
            .Total("₪ 180.00");

        var result = Parse(page);

        Assert.Empty(result.Errors);
        Assert.Equal(2, result.Records.Count);
        Assert.Equal(-30000, result.Records[0].Amount.Agorot);
        Assert.Equal(new InstallmentInfo(1, 3), result.Records[0].Installments);
        Assert.Equal(new InstallmentInfo(2, 3), result.Records[1].Installments);

        var (kept, dropped) = new InstallmentPolicy().Filter(result.Records);
        Assert.Single(kept);
        Assert.Equal(1, dropped);
    }

    [Fact]
    public void Both_ingested_sections_are_read_and_the_future_section_is_not()
    {
        var page = new Page()
            .Heading(ChargedBefore).Row("28/02/2031", "משיכת בדיקה", "₪ 200.00").Total("₪ 200.00")
            .Heading(Accumulated).Row("14/03/2031", "חנות בדיקה", "₪ 61.37").Total("₪ 61.37")
            .Heading(Future).Row("12/04/2031", "הוראת קבע עתידית", "₪ 888.00");

        var result = Parse(page);

        Assert.Empty(result.Errors);
        Assert.Equal(2, result.Records.Count);
        Assert.DoesNotContain(result.Records, r => r.Amount.Agorot == -88800);
    }

    [Fact]
    public void A_total_that_disagrees_with_the_rows_is_reported()
    {
        var page = new Page().Heading(Accumulated)
            .Row("14/03/2031", "חנות בדיקה", "₪ 61.37")
            .Total("₪ 62.37");

        var result = Parse(page);

        Assert.Single(result.Records);
        Assert.Contains(result.Errors, e => e.Contains("total") && e.Contains("doesn't match"));
    }

    [Fact]
    public void A_continued_heading_on_the_next_page_does_not_reset_the_section_total()
    {
        var first = new Page().Heading(Accumulated).Row("14/03/2031", "חנות בדיקה", "₪ 61.37");
        var second = new Page().Heading("המשך " + Accumulated)
            .Row("15/03/2031", "חנות שנייה", "₪ 33.45")
            .Total("₪ 94.82");

        var result = Parse(first, second);

        Assert.Empty(result.Errors);
        Assert.Equal(2, result.Records.Count);
    }

    [Fact]
    public void The_repeated_second_line_of_a_promotion_row_is_not_a_second_transaction()
    {
        var page = new Page().Heading(Accumulated)
            .Row("21/02/2031", "חנות מבצע", "₪ 100.00")
            .PromoRepeat("21/02/2031", "חנות מבצע")
            .Total("₪ 100.00");

        var result = Parse(page);

        Assert.Empty(result.Errors);
        Assert.Single(result.Records);
    }

    [Fact]
    public void A_row_with_no_amount_is_reported_not_guessed()
    {
        var page = new Page().Heading(Accumulated)
            .PromoRepeat("21/02/2031", "חנות לבד");

        var result = Parse(page);

        Assert.Empty(result.Records);
        Assert.Contains(result.Errors, e => e.Contains("no charge amount"));
    }

    [Fact]
    public void Two_identical_rows_are_both_kept_with_distinct_idempotency_keys()
    {
        var page = new Page().Heading(Accumulated)
            .Row("14/03/2031", "חנות בדיקה", "₪ 12.30")
            .Row("14/03/2031", "חנות בדיקה", "₪ 12.30")
            .Total("₪ 24.60");

        var result = Parse(page);

        Assert.Equal(2, result.Records.Count);
        Assert.NotEqual(IdempotencyKey.For(result.Records[0]), IdempotencyKey.For(result.Records[1]));
        Assert.Equal(IdempotencyKey.For(result.Records[1]), IdempotencyKey.For(Parse(page).Records[1]));
    }

    [Fact]
    public void A_document_with_no_transaction_rows_is_reported()
    {
        var result = Parse(new Page().Heading("דף חיוב חודשי"));

        Assert.Empty(result.Records);
        Assert.Contains(result.Errors, e => e.Contains("no transaction rows"));
    }

    [Fact]
    public void Visual_order_hebrew_is_restored_to_logical_order()
    {
        var glyphs = Rtl("חנות בדיקה אחת", 400, 100);

        Assert.Equal("חנות בדיקה אחת", CalStatementPdfParser.ToLogicalText(glyphs));
    }

    [Fact]
    public void Latin_words_keep_their_order_inside_a_hebrew_line()
    {
        var visual = Ltr("Store One", 100, 100).Concat(Rtl("חנות", 300, 100));

        Assert.Equal("חנות Store One", CalStatementPdfParser.ToLogicalText(visual));
    }

    [Fact]
    public void Only_pdf_files_are_claimed_and_a_corrupt_one_is_reported()
    {
        var parser = new CalStatementPdfParser();
        Assert.True(parser.CanParse("Jan_26_Cal.PDF"));
        Assert.False(parser.CanParse("statement.xlsx"));

        var path = Path.Combine(Path.GetTempPath(), $"cal-{Guid.NewGuid():N}.pdf");
        try
        {
            File.WriteAllText(path, "%PDF-1.4 not a real pdf");
            var result = parser.Parse(path, "cal");
            Assert.Empty(result.Records);
            Assert.NotEmpty(result.Errors);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
