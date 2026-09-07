using Finance.Core.Categorization;

namespace Finance.Tests.Core;

public class MerchantMatcherTests
{
    private sealed class FakeDictionary : IMerchantDictionary
    {
        private readonly Dictionary<string, string> _entries = new();
        public void Upsert(string merchantRaw, string categoryId) => _entries[merchantRaw] = categoryId;
        public string? Resolve(string merchantRaw) => _entries.GetValueOrDefault(merchantRaw);
        public IReadOnlyList<KeyValuePair<string, string>> Entries() => _entries.ToList();
    }

    [Fact]
    public void An_empty_dictionary_resolves_nothing()
    {
        var matcher = new MerchantMatcher(new FakeDictionary());

        Assert.Null(matcher.Resolve("שופרסל אונליין"));
    }

    [Fact]
    public void An_exact_merchant_match_wins()
    {
        var dict = new FakeDictionary();
        dict.Upsert("וולט", "delivery");
        var matcher = new MerchantMatcher(dict);

        Assert.Equal("delivery", matcher.Resolve("וולט"));
    }

    [Fact]
    public void An_exact_match_beats_a_contains_match()
    {
        var dict = new FakeDictionary();
        dict.Upsert("שופרסל", "groceries_generic");
        dict.Upsert("שופרסל אונליין", "groceries_online");
        var matcher = new MerchantMatcher(dict);

        Assert.Equal("groceries_online", matcher.Resolve("שופרסל אונליין"));
    }

    [Fact]
    public void When_only_contains_matches_exist_the_longest_one_wins()
    {
        var dict = new FakeDictionary();
        dict.Upsert("שופרסל", "groceries_generic");
        dict.Upsert("שופרסל דיל אקסטרה", "groceries_deal");
        var matcher = new MerchantMatcher(dict);

        // incoming string contains both known merchants; the more specific (longer) wins
        Assert.Equal("groceries_deal", matcher.Resolve("קניה שופרסל דיל אקסטרה סניף 123"));
    }

    [Fact]
    public void Matching_is_on_the_exact_scraped_string_not_a_loose_note()
    {
        var dict = new FakeDictionary();
        dict.Upsert("שופרסל אונליין", "groceries");
        var matcher = new MerchantMatcher(dict);

        Assert.Null(matcher.Resolve("Shufersal")); // English note shorthand never matches (spec §10.1)
    }
}
