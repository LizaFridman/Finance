using System;
using Finance.Core.Ingestion;

namespace Finance.Tests.Core;

public class IdempotencyKeyTests
{
    private static TransactionRecord Rec(
        string source = "leumi",
        string date = "2025-03-04",
        decimal shekels = -123.45m,
        string merchant = "  שופרסל אונליין  ",
        string? nativeId = null)
        => new(source, DateOnly.Parse(date), Finance.Core.Money.ToAgorot(shekels), merchant, nativeId);

    [Fact]
    public void Same_inputs_produce_the_same_key()
    {
        Assert.Equal(IdempotencyKey.For(Rec()), IdempotencyKey.For(Rec()));
    }

    [Fact]
    public void Merchant_whitespace_is_trimmed_before_hashing()
    {
        Assert.Equal(
            IdempotencyKey.For(Rec(merchant: "שופרסל אונליין")),
            IdempotencyKey.For(Rec(merchant: "   שופרסל אונליין   ")));
    }

    [Fact]
    public void Different_amount_produces_a_different_key()
    {
        Assert.NotEqual(
            IdempotencyKey.For(Rec(shekels: -123.45m)),
            IdempotencyKey.For(Rec(shekels: -123.46m)));
    }

    [Fact]
    public void A_native_id_is_used_verbatim_in_preference_to_the_derived_hash()
    {
        var key = IdempotencyKey.For(Rec(nativeId: "TXN-0007"));

        Assert.Equal("native:leumi:TXN-0007", key);
    }

    [Fact]
    public void With_a_native_id_the_other_fields_no_longer_affect_the_key()
    {
        Assert.Equal(
            IdempotencyKey.For(Rec(shekels: -1m, nativeId: "TXN-0007")),
            IdempotencyKey.For(Rec(shekels: -999m, nativeId: "TXN-0007")));
    }
}
