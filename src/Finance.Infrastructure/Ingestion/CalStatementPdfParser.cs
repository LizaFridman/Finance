using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Finance.Domain;
using Finance.Domain.Ingestion;
using UglyToad.PdfPig;

namespace Finance.Infrastructure.Ingestion;

/// <summary>One glyph of a PDF page: its text and horizontal advance (not ink box), with y the baseline (PDF points, y up).</summary>
public readonly record struct PositionedGlyph(string Text, double X0, double X1, double Y);

/// <summary>
/// Reads a Cal monthly statement PDF ("דף חיוב חודשי") into one
/// <see cref="TransactionRecord"/> per transaction row.
///
/// The PDF has no machine-readable table: every glyph is placed individually, and
/// Hebrew is laid out in visual (left-to-right) order. Rows are therefore rebuilt
/// from geometry. Each row is anchored by its transaction date in the rightmost
/// column; the other cells are read by fixed x-ranges (the columns are
/// right-aligned and stay put from page to page):
/// merchant, category, details (installment marker), and the two amounts
/// (charge in ₪, original transaction amount, possibly in a foreign currency).
///
/// Sections, found by their headings: "charged before the statement date" and
/// "accumulated up to the statement date" are ingested; "future charges" and
/// everything after it are skipped. Each section's own "total" line is checked
/// against the rows read, and a mismatch is reported, so a mis-read column
/// cannot pass silently. Charges in the statement are positive and credits
/// negative; amounts are negated onto the domain's "negative = outflow" convention.
/// For installments the first payment records the full original amount and later
/// ones are marked so <see cref="InstallmentPolicy"/> drops them.
///
/// The column positions are measured from a single statement; other months or
/// layout revisions are unverified, and anything that does not fit is reported
/// rather than guessed.
/// </summary>
public sealed class CalStatementPdfParser : IRawFeedParser
{
    private const double DateMinX = 521.0;
    private const double DateMaxX = 560.0;
    private const double MerchantMinX = 438.0;
    private const double MerchantMaxX = 521.0;
    private const double DetailsMinX = 301.0;
    private const double DetailsMaxX = 401.0;
    private const double AmountsMinX = 120.0;
    private const double AmountsMaxX = 301.0;

    private const double WordGap = 1.2;
    private const double SameLineTolerance = 1.0;
    private const double RowTolerance = 5.0;

    private const string FutureMarker = "פירוטעסקותלחיובעתידי";
    private const string ChargedBeforeMarker = "פירוטעסקותאשרחויבולפני";
    private const string AccumulatedMarker = "פירוטעסקותשנצברועד";
    private const string TotalMarker = "סהכלתאריך";

    private static readonly Regex DateRegex = new(@"^\d{2}/\d{2}/\d{4}$", RegexOptions.Compiled);
    private static readonly Regex MoneyRegex = new(
        @"^(-)?([₪$€£])(-)?([\d,]+(?:\.\d+)?)$", RegexOptions.Compiled);
    private static readonly Regex NumberRegex = new(@"^-?[\d,]+(?:\.\d+)?$", RegexOptions.Compiled);
    private static readonly Regex InstallmentRegex = new(
        @"(?:תשלום|קרדיט)\s*(\d+)\s*מ\s*-?\s*(\d+)", RegexOptions.Compiled);

    private enum Section { None, ChargedBefore, Accumulated, Future }

    private sealed record CurrencyAmount(char Symbol, decimal Value);

    public bool CanParse(string fileName) =>
        fileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase);

    public RawFeedParseResult Parse(string filePath, string sourceId)
    {
        var fileName = Path.GetFileName(filePath);
        try
        {
            using var document = PdfDocument.Open(filePath);
            var pages = document.GetPages()
                .Select(page => (IReadOnlyList<PositionedGlyph>)page.Letters
                    .Where(l => !string.IsNullOrWhiteSpace(l.Value))
                    .Select(l => new PositionedGlyph(l.Value, l.StartBaseLine.X, l.EndBaseLine.X, l.StartBaseLine.Y))
                    .ToList())
                .ToList();
            return ParsePages(pages, sourceId, fileName);
        }
        catch (Exception ex)
        {
            // PdfPig throws its own exception types on corrupt input; report, don't abort the run.
            return new RawFeedParseResult([], [$"{fileName}: could not read PDF ({ex.Message})"]);
        }
    }

    /// <summary>Geometry -> records, separate from PDF reading so tests can feed synthetic glyphs.</summary>
    public static RawFeedParseResult ParsePages(
        IReadOnlyList<IReadOnlyList<PositionedGlyph>> pages, string sourceId, string fileName)
    {
        var records = new List<TransactionRecord>();
        var errors = new List<string>();
        var duplicates = new Dictionary<string, int>();

        var section = Section.None;
        decimal runningCharge = 0;
        var rowsInSection = 0;
        var rowNumber = 0;
        (double Y, DateOnly Date, string Merchant)? previousRow = null;

        foreach (var page in pages)
        {
            var glyphs = page.Where(g => !string.IsNullOrWhiteSpace(g.Text)).ToList();
            var anchors = FindRowAnchors(glyphs);
            var lines = ClusterLines(glyphs);

            var events = new List<(double Y, Action Run)>();

            foreach (var line in lines)
            {
                var marker = Strip(ToLogicalText(line.Glyphs));
                if (marker.Contains(FutureMarker, StringComparison.Ordinal))
                    events.Add((line.Y, () => section = Section.Future));
                else if (marker.Contains(ChargedBeforeMarker, StringComparison.Ordinal))
                    events.Add((line.Y, () => StartSection(Section.ChargedBefore)));
                else if (marker.Contains(AccumulatedMarker, StringComparison.Ordinal))
                    events.Add((line.Y, () => StartSection(Section.Accumulated)));
                else if (marker.Contains(TotalMarker, StringComparison.Ordinal))
                {
                    var lineGlyphs = line.Glyphs;
                    events.Add((line.Y, () => CheckTotal(lineGlyphs)));
                }
            }

            foreach (var anchor in anchors)
            {
                var rowGlyphs = glyphs
                    .Where(g => Math.Abs(g.Y - anchor.Y) <= RowTolerance
                                && NearestAnchorY(anchors, g.Y) == anchor.Y)
                    .ToList();
                events.Add((anchor.Y, () => ReadRow(anchor, rowGlyphs)));
            }

            foreach (var ev in events.OrderByDescending(e => e.Y))
                ev.Run();
        }

        if (records.Count == 0 && errors.Count == 0)
            errors.Add($"{fileName}: no transaction rows found (is this a Cal monthly statement?)");

        return new RawFeedParseResult(records, errors);

        void StartSection(Section next)
        {
            // A "continued" heading at the top of each page names the same section; only a
            // genuinely new section starts a fresh running total.
            if (next == section)
                return;
            section = next;
            runningCharge = 0;
            rowsInSection = 0;
        }

        void CheckTotal(IReadOnlyList<PositionedGlyph> lineGlyphs)
        {
            if (section is Section.None or Section.Future)
                return;

            var total = FindMoney(SplitWords(lineGlyphs.Where(g => g.X0 < AmountsMaxX)));
            if (total.Count > 0 && total[0].Value != runningCharge)
                errors.Add($"{fileName}: a statement section's total ({total[0].Value}) doesn't match the " +
                           $"{rowsInSection} rows read ({runningCharge}) — a column was probably mis-read");
            runningCharge = 0;
            rowsInSection = 0;
        }

        void ReadRow(Anchor anchor, List<PositionedGlyph> rowGlyphs)
        {
            if (section is Section.None or Section.Future)
                return;

            var money = FindMoney(SplitWords(rowGlyphs.Where(g => g.X0 >= AmountsMinX && g.X0 < AmountsMaxX)));
            var merchant = ToLogicalText(rowGlyphs.Where(g => g.X0 >= MerchantMinX && g.X0 < MerchantMaxX)).Trim();
            var details = ToLogicalText(rowGlyphs.Where(g => g.X0 >= DetailsMinX && g.X0 < DetailsMaxX));

            if (!DateOnly.TryParseExact(anchor.Text, "dd/MM/yyyy", CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out var date))
            {
                errors.Add($"{fileName} row {rowNumber}: unparseable date '{anchor.Text}'");
                return;
            }

            // A row carrying a promotion note is printed over two lines: the second repeats the
            // same date and merchant with no amounts. It is the same transaction, not a new one.
            if (money.Count == 0 && previousRow is { } prev
                && prev.Y - anchor.Y <= 2 * RowTolerance && prev.Date == date && prev.Merchant == merchant)
                return;

            rowNumber++;
            previousRow = (anchor.Y, date, merchant);

            if (merchant.Length == 0)
            {
                errors.Add($"{fileName} row {rowNumber}: missing merchant");
                return;
            }

            if (money.Count == 0)
            {
                errors.Add($"{fileName} row {rowNumber}: no charge amount found");
                return;
            }

            var charge = money[0];
            if (charge.Symbol != '₪')
            {
                errors.Add($"{fileName} row {rowNumber}: charge amount is not in ₪");
                return;
            }

            var original = money.Count > 1 ? money[^1] : charge;
            runningCharge += charge.Value;
            rowsInSection++;

            var installments = ReadInstallments(details);
            var shekels = charge.Value;
            if (installments is { Number: 1 } && original.Symbol == '₪')
                shekels = original.Value; // first payment carries the full original amount (spec §8)

            var record = new TransactionRecord(
                SourceId: sourceId,
                Date: date,
                Amount: Money.FromShekels(-shekels),
                MerchantRaw: merchant,
                NativeId: null,
                Installments: installments);

            // Two genuinely identical rows would share an idempotency key and the second would be
            // lost; a stable occurrence number keeps both while staying re-run safe.
            var key = $"{date:yyyy-MM-dd}|{record.Amount.Agorot}|{merchant}";
            duplicates[key] = duplicates.GetValueOrDefault(key) + 1;
            if (duplicates[key] > 1)
                record = record with { NativeId = $"dup{duplicates[key]}:{key}" };

            records.Add(record);
        }
    }

    private sealed record Anchor(double Y, string Text);
    private sealed record Line(double Y, IReadOnlyList<PositionedGlyph> Glyphs);

    private static List<Anchor> FindRowAnchors(IEnumerable<PositionedGlyph> glyphs)
    {
        var anchors = new List<Anchor>();
        var dateZone = glyphs.Where(g => g.X0 >= DateMinX - 0.5 && g.X1 <= DateMaxX).ToList();
        foreach (var line in ClusterLines(dateZone))
        {
            var text = string.Concat(line.Glyphs.OrderBy(g => g.X0).Select(g => g.Text));
            if (DateRegex.IsMatch(text))
                anchors.Add(new Anchor(line.Y, text));
        }

        return anchors;
    }

    private static double NearestAnchorY(List<Anchor> anchors, double y) =>
        anchors.OrderBy(a => Math.Abs(a.Y - y)).First().Y;

    private static List<Line> ClusterLines(IEnumerable<PositionedGlyph> glyphs)
    {
        var lines = new List<Line>();
        var current = new List<PositionedGlyph>();
        var currentY = 0.0;

        foreach (var g in glyphs.OrderByDescending(g => g.Y))
        {
            if (current.Count > 0 && Math.Abs(g.Y - currentY) > SameLineTolerance)
            {
                lines.Add(new Line(currentY, current));
                current = new List<PositionedGlyph>();
            }

            if (current.Count == 0)
                currentY = g.Y;
            current.Add(g);
        }

        if (current.Count > 0)
            lines.Add(new Line(currentY, current));
        return lines;
    }

    private sealed record Word(string Text, double X0, double X1);

    private static List<Word> SplitWords(IEnumerable<PositionedGlyph> glyphs)
    {
        var words = new List<Word>();
        var sb = new StringBuilder();
        double x0 = 0, x1 = 0;

        foreach (var g in glyphs.OrderBy(g => g.X0))
        {
            if (sb.Length > 0 && g.X0 - x1 >= WordGap)
            {
                words.Add(new Word(sb.ToString(), x0, x1));
                sb.Clear();
            }

            if (sb.Length == 0)
                x0 = g.X0;
            sb.Append(g.Text);
            x1 = Math.Max(x1, g.X1);
        }

        if (sb.Length > 0)
            words.Add(new Word(sb.ToString(), x0, x1));
        return words;
    }

    /// <summary>Currency amounts in left-to-right order, from "₪ 12.34", "₪12.34" or "$ -5.67".</summary>
    private static List<CurrencyAmount> FindMoney(List<Word> words)
    {
        var found = new List<CurrencyAmount>();
        for (var i = 0; i < words.Count; i++)
        {
            var text = words[i].Text;
            if (text.Length == 1 && "₪$€£".Contains(text[0]) && i + 1 < words.Count
                && NumberRegex.IsMatch(words[i + 1].Text))
            {
                found.Add(new CurrencyAmount(text[0], ParseNumber(words[i + 1].Text)));
                i++;
                continue;
            }

            var match = MoneyRegex.Match(text);
            if (match.Success)
            {
                var negative = match.Groups[1].Success || match.Groups[3].Success;
                var value = ParseNumber(match.Groups[4].Value);
                found.Add(new CurrencyAmount(match.Groups[2].Value[0], negative ? -value : value));
            }
        }

        return found;
    }

    private static decimal ParseNumber(string text) =>
        decimal.Parse(text.Replace(",", string.Empty), NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
            CultureInfo.InvariantCulture);

    private static InstallmentInfo? ReadInstallments(string detailsLogicalText)
    {
        var match = InstallmentRegex.Match(detailsLogicalText);
        return match.Success
            ? new InstallmentInfo(int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture),
                int.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture))
            : null;
    }

    private static string Strip(string text) => new(text.Where(char.IsLetterOrDigit).ToArray());

    private static bool IsHebrew(char c) => c is >= '֐' and <= '׿';

    /// <summary>
    /// Glyphs laid out left-to-right in visual order -> logical reading order of a right-to-left
    /// line: Hebrew words are reversed, Latin/digit runs keep their order, and word order flips.
    /// </summary>
    internal static string ToLogicalText(IEnumerable<PositionedGlyph> glyphs)
    {
        var groups = new List<string>();
        var ltr = new List<string>();

        void FlushLtr()
        {
            if (ltr.Count == 0)
                return;
            groups.Add(string.Join(' ', ltr));
            ltr.Clear();
        }

        foreach (var word in SplitWords(glyphs))
        {
            if (word.Text.Any(IsHebrew))
            {
                FlushLtr();
                groups.Add(ReorderMixedWord(word.Text));
            }
            else if (word.Text.Any(char.IsLetterOrDigit))
            {
                ltr.Add(word.Text);
            }
            else
            {
                FlushLtr();
                groups.Add(word.Text);
            }
        }

        FlushLtr();
        groups.Reverse();
        return string.Join(' ', groups);
    }

    /// <summary>One visual word that contains Hebrew -> logical order (reverse Hebrew runs, flip run order).</summary>
    private static string ReorderMixedWord(string visual)
    {
        var classes = visual.Select(c => IsHebrew(c) ? 'H' : char.IsLetterOrDigit(c) ? 'X' : 'N').ToArray();
        for (var i = 0; i < classes.Length; i++)
        {
            if (classes[i] != 'N')
                continue;
            var prev = '\0';
            for (var j = i - 1; j >= 0; j--)
                if (classes[j] != 'N') { prev = classes[j]; break; }
            var next = '\0';
            for (var j = i + 1; j < classes.Length; j++)
                if (classes[j] != 'N') { next = classes[j]; break; }
            classes[i] = prev == next || prev == '\0' ? (next == '\0' ? 'X' : next) : prev;
        }

        var runs = new List<(char Class, string Text)>();
        var start = 0;
        for (var i = 1; i <= visual.Length; i++)
        {
            if (i < visual.Length && classes[i] == classes[start])
                continue;
            runs.Add((classes[start], visual[start..i]));
            start = i;
        }

        runs.Reverse();
        return string.Concat(runs.Select(r => r.Class == 'H' ? new string(r.Text.Reverse().ToArray()) : r.Text));
    }
}
