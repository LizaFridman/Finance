using Finance.Infrastructure;

namespace Finance.Tests.Infrastructure;

public class ConfigStoreTests
{
    [Fact]
    public void YearStartMonth_defaults_to_January()
    {
        using var t = new TempDatabase();
        var config = new ConfigStore(t.Db);

        Assert.Equal(1, config.GetYearStartMonth());
    }

    [Fact]
    public void YearStartMonth_is_overridable()
    {
        using var t = new TempDatabase();
        var config = new ConfigStore(t.Db);

        config.SetYearStartMonth(4); // e.g. an April fiscal-year start

        Assert.Equal(4, config.GetYearStartMonth());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(13)]
    [InlineData(-1)]
    public void SetYearStartMonth_rejects_out_of_range(int month)
    {
        using var t = new TempDatabase();
        var config = new ConfigStore(t.Db);

        Assert.Throws<System.ArgumentOutOfRangeException>(() => config.SetYearStartMonth(month));
    }

    [Fact]
    public void Get_returns_null_for_unknown_key()
    {
        using var t = new TempDatabase();
        var config = new ConfigStore(t.Db);

        Assert.Null(config.Get("no-such-key"));
    }
}
