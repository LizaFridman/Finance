using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using UglyToad.PdfPig;

namespace Finance.Core.Ingestion;

/// <summary>
/// Reads a single utility/recurring bill PDF (water, electricity, גז, וועד בית,
/// Partner — spec §5) into exactly one <see cref="TransactionRecord"/>: a bill is
/// one charge for a billing period, not itemized rows. <see cref="NativeId"/> is
/// the filename, so re-ingesting the same PDF is a no-op via the existing
/// idempotency path (<see cref="IdempotencyKey"/>).
///
/// The amount/date patterns below were written from the Hebrew/English phrasing
/// described in the project's own docs (docs/missing-data-report.md), not from
/// real sample files — none exist in this sandbox; they're gitignored by design
/// and only ever live on the real host machine. Expect to add patterns the first
/// time this runs against an actual bill; a mismatch throws loudly rather than
/// silently producing a wrong amount or date.
/// </summary>
public sealed class UtilityBillPdfParser : IRawFeedParser
{
    private static readonly Regex[] AmountPatterns =
    {
        new(@"סה[""']?כ\s*לתשלום[:\s]*([\d,]+\.?\d*)", RegexOptions.Compiled),
        new(@"לתשלום[:\s]*([\d,]+\.?\d*)\s*(?:ש[""']?ח|₪)", RegexOptions.Compiled),
        new(@"(?:total\s+(?:due|amount)|amount\s+due)[:\s]*(?:nis|ils|₪)?\s*([\d,]+\.?\d*)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase),
        new(@"total[:\s]*(?:nis|ils|₪)\s*([\d,]+\.?\d*)", RegexOptions.Compiled | RegexOptions.IgnoreCase),
    };

    private static readonly Regex[] DatePatterns =
    {
        new(@"תאריך\s*לתשלום[:\s]*(\d{1,2}[./]\d{1,2}[./]\d{2,4})", RegexOptions.Compiled),
        new(@"לתשלום\s*עד[:\s]*(\d{1,2}[./]\d{1,2}[./]\d{2,4})", RegexOptions.Compiled),
        new(@"עד\s*תאריך[:\s]*(\d{1,2}[./]\d{1,2}[./]\d{2,4})", RegexOptions.Compiled),
        new(@"due\s*date[:\s]*(\d{1,2}[./]\d{1,2}[./]\d{2,4})", RegexOptions.Compiled | RegexOptions.IgnoreCase),
        new(@"(\d{1,2}[./]\d{1,2}[./]\d{2,4})", RegexOptions.Compiled),
    };

    public bool CanParse(string fileName) =>
        fileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase);

    public IReadOnlyList<TransactionRecord> Parse(string filePath, string sourceId)
    {
        var text = ExtractText(filePath);
        return new[] { ParseBillText(text, sourceId, Path.GetFileName(filePath)) };
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
    /// Pure text -> record conversion, kept separate from <see cref="ExtractText"/>
    /// so it can be unit-tested with literal strings instead of real PDF bytes.
    /// </summary>
    internal static TransactionRecord ParseBillText(string text, string sourceId, string fileName)
    {
        var amount = MatchFirst(AmountPatterns, text, fileName,
            "amount due (looked for \"סה\"כ לתשלום\" / \"לתשלום\" / \"total due\" phrasing)");
        var dateText = MatchFirst(DatePatterns, text, fileName,
            "a due/payment date (dd/mm/yyyy or dd.mm.yyyy)");

        var amountValue = decimal.Parse(
            amount.Replace(",", string.Empty), CultureInfo.InvariantCulture);
        var date = ParseDate(dateText, fileName);

        return new TransactionRecord(
            SourceId: sourceId,
            Date: date,
            AmountAgorot: -Money.ToAgorot(amountValue), // a bill is always an outflow
            MerchantRaw: sourceId,
            NativeId: Path.GetFileNameWithoutExtension(fileName));
    }

    private static string MatchFirst(Regex[] patterns, string text, string fileName, string lookingFor)
    {
        foreach (var pattern in patterns)
        {
            var match = pattern.Match(text);
            if (match.Success)
                return match.Groups[1].Value;
        }

        throw new InvalidOperationException(
            $"'{fileName}': couldn't find {lookingFor} in the extracted PDF text. " +
            "This bill's layout doesn't match the patterns UtilityBillPdfParser knows about " +
            "yet — add a pattern for its actual wording rather than guessing at the value.");
    }

    private static DateOnly ParseDate(string dateText, string fileName)
    {
        var separator = dateText.Contains('.') ? '.' : '/';
        var parts = dateText.Split(separator);
        if (parts.Length != 3 ||
            !int.TryParse(parts[0], out var day) ||
            !int.TryParse(parts[1], out var month) ||
            !int.TryParse(parts[2], out var year))
        {
            throw new InvalidOperationException(
                $"'{fileName}': found date text '{dateText}' but couldn't parse it as dd/mm/yyyy.");
        }

        if (year < 100)
            year += 2000;

        return new DateOnly(year, month, day);
    }
}
