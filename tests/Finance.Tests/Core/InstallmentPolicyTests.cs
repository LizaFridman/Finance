using System;
using Finance.Core.Ingestion;

namespace Finance.Tests.Core;

/// <summary>
/// The scraper's <c>combineInstallments</c> already collapses a series to its
/// first installment; <see cref="InstallmentPolicy"/> is the C# safety net that
/// asserts no later-installment rows slipped through (spec §8).
/// </summary>
public class InstallmentPolicyTests
{
    private static TransactionRecord Normal()
        => new("cal", new DateOnly(2025, 5, 1), -5000, "רגיל");

    private static TransactionRecord Installment(int number, int total)
        => new("cal", new DateOnly(2025, 5, 1), -5000, "בתשלומים",
               Installments: new InstallmentInfo(number, total));

    [Fact]
    public void Non_installment_transactions_are_kept()
    {
        var (kept, dropped) = new InstallmentPolicy().Filter(new[] { Normal() });

        Assert.Single(kept);
        Assert.Equal(0, dropped);
    }

    [Fact]
    public void The_first_installment_is_kept_and_carries_the_full_amount()
    {
        var (kept, dropped) = new InstallmentPolicy().Filter(new[] { Installment(1, 12) });

        Assert.Single(kept);
        Assert.Equal(-5000, kept[0].AmountAgorot);
        Assert.Equal(0, dropped);
    }

    [Fact]
    public void Later_installments_of_a_series_are_discarded()
    {
        var (kept, dropped) = new InstallmentPolicy().Filter(new[]
        {
            Installment(1, 3),
            Installment(2, 3),
            Installment(3, 3),
        });

        Assert.Single(kept);
        Assert.Equal(2, dropped);
    }
}
