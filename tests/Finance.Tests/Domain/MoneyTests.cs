using Finance.Domain;

namespace Finance.Tests.Domain;

public class MoneyTests
{
    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 100)]
    [InlineData(-123.45, -12345)]
    [InlineData(0.005, 1)]   // rounds away from zero
    [InlineData(-0.005, -1)]
    public void FromShekels_rounds_to_the_nearest_agora_away_from_zero(decimal shekels, long agorot)
    {
        Assert.Equal(new Money(agorot), Money.FromShekels(shekels));
    }

    [Fact]
    public void Shekels_is_the_exact_inverse_for_whole_agorot()
    {
        Assert.Equal(-123.45m, new Money(-12345).Shekels);
    }

    [Fact]
    public void Inflow_and_outflow_follow_the_sign()
    {
        Assert.True(new Money(500).IsInflow);
        Assert.True(new Money(-500).IsOutflow);
        Assert.False(Money.Zero.IsInflow);
        Assert.False(Money.Zero.IsOutflow);
    }

    [Fact]
    public void Arithmetic_and_comparison_operate_on_agorot()
    {
        Assert.Equal(new Money(300), new Money(500) + new Money(-200));
        Assert.Equal(new Money(-500), -new Money(500));
        Assert.True(new Money(-200) < new Money(-100));
        Assert.True(new Money(100) >= new Money(100));
    }
}
