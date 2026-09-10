namespace Finance.Domain.Categorization;

/// <summary>
/// Resolves a scraped merchant string to a category id against an in-memory
/// snapshot of Layer 1, with the match priority from spec §10.3:
///   1. exact merchant match;
///   2. otherwise the <em>longest</em> known merchant string contained in the
///      raw name (so "שופרסל דיל אקסטרה" beats a bare "שופרסל");
///   3. otherwise no match — the transaction stays for Layer 2 review.
///
/// Build one per sweep (see <see cref="FromDictionary"/>) so Layer 1 is read
/// once, not once per transaction.
/// </summary>
public sealed class MerchantMatcher
{
    private readonly Dictionary<string, string> _exact;
    private readonly IReadOnlyList<KeyValuePair<string, string>> _byLengthDescending;

    public MerchantMatcher(IEnumerable<KeyValuePair<string, string>> entries)
    {
        var list = entries.ToList();

        _exact = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (merchant, categoryId) in list)
            _exact[merchant] = categoryId; // last write wins, matching an upsert

        _byLengthDescending = list.OrderByDescending(e => e.Key.Length).ToList();
    }

    /// <summary>Snapshot the whole dictionary now and match against that snapshot.</summary>
    public static MerchantMatcher FromDictionary(IMerchantDictionary dictionary) =>
        new(dictionary.Entries());

    public string? Resolve(string merchantRaw)
    {
        var needle = merchantRaw.Trim();

        if (_exact.TryGetValue(needle, out var exact))
            return exact;

        foreach (var (known, categoryId) in _byLengthDescending)
            if (needle.Contains(known, StringComparison.Ordinal))
                return categoryId;

        return null;
    }
}
