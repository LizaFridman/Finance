using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Finance.Domain.Ingestion;

/// <summary>
/// Produces the stable per-transaction key used as <c>transactions.id</c>, so a
/// re-run inserts only rows it hasn't seen (spec §7.1). The key does not depend
/// on how a file arrived — scraper or hand-drop are identical.
/// </summary>
public static class IdempotencyKey
{
    public static string For(TransactionRecord record)
    {
        // Special-case: when the source supplies its own transaction id it is
        // already globally unique for that source, so use it verbatim rather
        // than the derived hash (spec §7.1).
        if (!string.IsNullOrWhiteSpace(record.NativeId))
            return $"native:{record.SourceId}:{record.NativeId.Trim()}";

        var basis = string.Join(
            '|',
            record.SourceId,
            record.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            record.Amount.Agorot.ToString(CultureInfo.InvariantCulture),
            record.MerchantRaw.Trim());

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(basis));
        return Convert.ToHexStringLower(hash);
    }
}
