using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Finance.Domain;
using Finance.Domain.Ingestion;
using UglyToad.PdfPig;

namespace Finance.Infrastructure.Ingestion;

/// <summary>
/// Reads a single utility/recurring bill PDF (water, electricity, גז, וועד בית,
/// Partner — spec §5) into exactly one <see cref="TransactionRecord"/>: a bill is
/// one charge for a billing period, not itemized rows. The record's native id is
/// the filename, so re-ingesting the same PDF is a no-op via
/// <see cref="IdempotencyKey"/>.
///
/// The amount/date patterns were written from the Hebrew/English phrasing
/// described in the project docs, not from real bills — none exist in a repo
/// clone (they're gitignored by design). PdfPig can also emit Hebrew in visual
/// (reversed) order, so each labeled pattern has a mirrored variant. Expect to
/// add patterns the first time this runs on an actual bill. It never guesses: no
/// labeled amount or due date means no record and a reported error, not a
/// plausible-looking wrong number.
/// </summary>
public sealed class UtilityBillPdfParser : IRawFeedParser
{
    private const string Number = @"([\d,]+\.?\d*)";
    private const string DateText = @"(\d{1,2}[./]\d{1,2}[./]\d{2,4})";

    private static readonly Regex[] AmountPatterns =
    [
        new($@"סה[""']?כ\s*לתשלום[:\s]*{Number}", RegexOptions.Compiled),
        new($@"{Number}\s*:?\s*םולשתל\s*כ[""']?הס", RegexOptions.Compiled),
        new($@"לתשלום[:\s]*{Number}\s*(?:ש[""']?ח|₪)", RegexOptions.Compiled),
        new($@"(?:total\s+(?:due|amount)|amount\s+due)[:\s]*(?:nis|ils|₪)?\s*{Number}",
            RegexOptions.Compiled | RegexOptions.IgnoreCase),
    ];

    private static readonly Regex[] DatePatterns =
    [
        new($@"תאריך\s*לתשלום[:\s]*{DateText}", RegexOptions.Compiled),
        new($@"{DateText}\s*:?\s*םולשתל\s*ךיראת", RegexOptions.Compiled),
        new($@"לתשלום\s*עד[:\s]*{DateText}", RegexOptions.Compiled),
        new($@"עד\s*תאריך[:\s]*{DateText}", RegexOptions.Compiled),
        new($@"due\s*date[:\s]*{DateText}", RegexOptions.Compiled | RegexOptions.IgnoreCase),
    ];

    private static readonly string[] DateFormats = ["d/M/yyyy", "d.M.yyyy", "d/M/yy", "d.M.yy"];

    public bool CanParse(string fileName) =>
        fileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase);

    public RawFeedParseResult Parse(string filePath, string sourceId)
    {
        var fileName = Path.GetFileName(filePath);

        string text;
        try
        {
            text = ExtractText(filePath);
        }
        catch (Exception ex)
        {
            // PdfPig throws its own exception types on corrupt input; any failure to
            // read this one file is reported, not fatal to the ingestion run.
            return new RawFeedParseResult([], [$"{fileName}: could not read PDF ({ex.Message})"]);
        }

        return ParseBillText(text, sourceId, fileName);
    }

    private static string ExtractText(string filePath)
    {
        using var document = PdfDocument.Open(filePath);
        var builder = new StringBuilder();
        foreach (var page in document.GetPages())
            builder.AppendLine(page.Text);
        return builder.ToString();
    }

    /// <summary>
    /// Pure text -> result conversion, separate from PDF reading so it can be
    /// tested with literal strings instead of real PDF bytes.
    /// </summary>
    public static RawFeedParseResult ParseBillText(string text, string sourceId, string fileName)
    {
        var errors = new List<string>();

        decimal amount = 0;
        var amountText = MatchFirst(AmountPatterns, text);
        if (amountText is null)
            errors.Add($"{fileName}: no amount due found (looked for \"סה\"כ לתשלום\" / \"לתשלום\" / \"total due\" wording)");
        else if (!decimal.TryParse(amountText.Replace(",", string.Empty), NumberStyles.AllowDecimalPoint,
                     CultureInfo.InvariantCulture, out amount))
            errors.Add($"{fileName}: found amount text '{amountText}' but couldn't parse it");

        DateOnly date = default;
        var dateText = MatchFirst(DatePatterns, text);
        if (dateText is null)
            errors.Add($"{fileName}: no labeled due/payment date found");
        else if (!DateOnly.TryParseExact(dateText, DateFormats, CultureInfo.InvariantCulture,
                     DateTimeStyles.None, out date))
            errors.Add($"{fileName}: found date text '{dateText}' but couldn't parse it as dd/mm/yyyy");

        if (errors.Count > 0)
            return new RawFeedParseResult([], errors);

        var record = new TransactionRecord(
            SourceId: sourceId,
            Date: date,
            Amount: Money.FromShekels(-amount), // a bill is always an outflow
            MerchantRaw: sourceId,
            NativeId: Path.GetFileNameWithoutExtension(fileName));

        return RawFeedParseResult.FromRecords([record]);
    }

    private static string? MatchFirst(Regex[] patterns, string text)
    {
        foreach (var pattern in patterns)
        {
            var match = pattern.Match(text);
            if (match.Success)
                return match.Groups[1].Value;
        }

        return null;
    }
}
