namespace Finance.Core;

/// <summary>
/// Conversions between shekels (<see cref="decimal"/>, the boundary type) and
/// agorot (<see cref="long"/>, how amounts are stored — see the sign-convention
/// note in schema.sql / flagged decision 1). 1 shekel = 100 agorot.
/// </summary>
public static class Money
{
    public static long ToAgorot(decimal shekels) =>
        (long)decimal.Round(shekels * 100m, MidpointRounding.AwayFromZero);

    public static decimal ToShekels(long agorot) => agorot / 100m;
}
