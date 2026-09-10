namespace Finance.Domain.Categorization;

/// <summary>
/// Resolves a scraped merchant string to a category id using Layer 1, with the
/// match priority from spec §10.3:
///   1. exact merchant match;
///   2. otherwise the <em>longest</em> known merchant string contained in the
///      raw name (so "שופרסל דיל אקסטרה" beats a bare "שופרסל");
///   3. otherwise no match — the transaction stays for Layer 2 review.
/// </summary>
public sealed class MerchantMatcher
{
    private readonly IMerchantDictionary _dictionary;

    public MerchantMatcher(IMerchantDictionary dictionary)
    {
        _dictionary = dictionary;
    }

    public string? Resolve(string merchantRaw)
    {
        var needle = merchantRaw.Trim();

        // 1. exact
        var exact = _dictionary.Resolve(needle);
        if (exact is not null)
            return exact;

        // 2. longest contains-match
        string? best = null;
        var bestLength = -1;
        foreach (var (known, categoryId) in _dictionary.Entries())
        {
            if (known.Length > bestLength && needle.Contains(known, StringComparison.Ordinal))
            {
                best = categoryId;
                bestLength = known.Length;
            }
        }

        return best;
    }
}
