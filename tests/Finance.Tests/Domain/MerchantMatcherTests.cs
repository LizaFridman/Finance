using System.Collections.Generic;
using System.Linq;
using Finance.Domain.Categorization;

namespace Finance.Tests.Domain;

public class MerchantMatcherTests
{
    private static MerchantMatcher Matcher(params (string Merchant, string Category)[] entries) =>
        new(entries.Select(e => new KeyValuePair<string, string>(e.Merchant, e.Category)));

    [Fact]
    public void An_empty_dictionary_resolves_nothing()
    {
        Assert.Null(Matcher().Resolve("שופרסל אונליין"));
    }

    [Fact]
    public void An_exact_merchant_match_wins()
    {
        Assert.Equal("delivery", Matcher(("וולט", "delivery")).Resolve("וולט"));
    }

    [Fact]
    public void An_exact_match_beats_a_contains_match()
    {
        var matcher = Matcher(
            ("שופרסל", "groceries_generic"),
            ("שופרסל אונליין", "groceries_online"));

        Assert.Equal("groceries_online", matcher.Resolve("שופרסל אונליין"));
    }

    [Fact]
    public void When_only_contains_matches_exist_the_longest_one_wins()
    {
        var matcher = Matcher(
            ("שופרסל", "groceries_generic"),
            ("שופרסל דיל אקסטרה", "groceries_deal"));

        // incoming string contains both known merchants; the more specific (longer) wins
        Assert.Equal("groceries_deal", matcher.Resolve("קניה שופרסל דיל אקסטרה סניף 123"));
    }

    [Fact]
    public void Matching_is_on_the_exact_scraped_string_not_a_loose_note()
    {
        var matcher = Matcher(("שופרסל אונליין", "groceries"));

        Assert.Null(matcher.Resolve("Shufersal")); // English note shorthand never matches (spec §10.1)
    }
}
