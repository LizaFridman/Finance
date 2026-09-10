namespace Finance.Domain.Categorization;

/// <summary>
/// Layer 1 (spec §10). Starts empty; every entry is written back from a
/// confirmed Layer 2 decision, keyed on the exact scraped merchant string
/// (Hebrew, as it appears in the source) — never on a free-text note.
/// </summary>
public interface IMerchantDictionary
{
    /// <summary>Exact-match lookup only. <see cref="MerchantMatcher"/> adds the contains fallback.</summary>
    string? Resolve(string merchantRaw);

    void Upsert(string merchantRaw, string categoryId);

    IReadOnlyList<KeyValuePair<string, string>> Entries();
}
