using LineSight.Core.Oee;

namespace LineSight.Tests.Oee;

/// <summary>End-to-end OEE = A x P x Q, including edge cases and the performance flag.</summary>
public class OeeCalculateTests
{
    [Fact]
    public void Calculate_NormalShift_MultipliesTheThreeFactors()
    {
        // 1 hour planned, 54 min running -> A = 0.9
        // ideal 30s, 102 parts in 3240s  -> P = 3060/3240 = 0.9444
        // 98 good of 102                 -> Q = 0.9608
        var input = new OeeInput(PlannedProductionTimeSeconds: 3600, RunTimeSeconds: 3240,
            IdealCycleTimeSeconds: 30, GoodCount: 98, ScrapCount: 4);

        var result = OeeCalculator.Calculate(input);

        Assert.Equal(0.9, result.Availability, precision: 4);
        Assert.Equal(0.9444, result.Performance, precision: 4);
        Assert.Equal(0.9608, result.Quality, precision: 4);
        Assert.Equal(0.8167, result.Oee, precision: 4);   // 0.9 * 0.94444 * 0.96078
        Assert.False(result.PerformanceAboveIdeal);
    }

    [Fact]
    public void Calculate_PerfectProduction_Is100Percent()
    {
        var input = new OeeInput(3600, 3600, 30, GoodCount: 120, ScrapCount: 0);

        var result = OeeCalculator.Calculate(input);

        Assert.Equal(new OeeResult(1.0, 1.0, 1.0, 1.0, false), result);
    }

    [Fact]
    public void Calculate_ZeroRunTime_ReturnsZeroOee()
    {
        var input = new OeeInput(3600, RunTimeSeconds: 0, 30, GoodCount: 0, ScrapCount: 0);

        var result = OeeCalculator.Calculate(input);

        Assert.Equal(0.0, result.Availability);
        Assert.Equal(0.0, result.Performance);
        Assert.Equal(0.0, result.Oee);
    }

    [Fact]
    public void Calculate_ZeroTotalCount_ReturnsZeroOee()
    {
        // Station "ran" but produced nothing (e.g. starved but reporting Running).
        var input = new OeeInput(3600, 3600, 30, GoodCount: 0, ScrapCount: 0);

        var result = OeeCalculator.Calculate(input);

        Assert.Equal(0.0, result.Quality);
        Assert.Equal(0.0, result.Oee);
    }

    [Fact]
    public void Calculate_AllScrap_ReturnsZeroQualityAndZeroOee()
    {
        var input = new OeeInput(3600, 3600, 30, GoodCount: 0, ScrapCount: 120);

        var result = OeeCalculator.Calculate(input);

        Assert.Equal(1.0, result.Performance);   // the machine was fast...
        Assert.Equal(0.0, result.Quality);       // ...but made nothing sellable
        Assert.Equal(0.0, result.Oee);
    }

    [Fact]
    public void Calculate_PerformanceAbove100Percent_IsCalculatedAndFlagged()
    {
        // 150 parts at a 30s "ideal" in one hour is impossible -> ideal cycle time is set wrong.
        var input = new OeeInput(3600, 3600, 30, GoodCount: 150, ScrapCount: 0);

        var result = OeeCalculator.Calculate(input);

        Assert.Equal(1.25, result.Performance, precision: 4);
        Assert.True(result.PerformanceAboveIdeal);
    }

    [Theory]
    [InlineData(3600, 1200, 1.0, 1200, 0, 0.3333)]   // A = 1/3 -> rounds down
    [InlineData(3600, 2400, 1.0, 2400, 0, 0.6667)]   // A = 2/3 -> rounds up
    [InlineData(1000, 999.99, 1.0, 999, 0, 0.999)]   // tiny losses survive rounding
    public void Calculate_RoundsOeeToFourDecimals(
        double planned, double run, double ideal, int good, int scrap, double expectedOee)
    {
        var result = OeeCalculator.Calculate(new OeeInput(planned, run, ideal, good, scrap));

        Assert.Equal(expectedOee, result.Oee);   // exact compare: value must already be rounded
    }

    [Fact]
    public void Calculate_OeeIsComputedFromUnroundedFactors()
    {
        // A = P = 0.99996, Q = 1. Rounding A and P first would give 1.0 x 1.0 = 1.0 (a fake 100%);
        // the true product is 0.99992 -> 0.9999.
        var input = new OeeInput(PlannedProductionTimeSeconds: 100_000, RunTimeSeconds: 99_996,
            IdealCycleTimeSeconds: 0.99996, GoodCount: 99_996, ScrapCount: 0);

        var result = OeeCalculator.Calculate(input);

        Assert.Equal(1.0, result.Availability);   // displayed factor rounds up...
        Assert.Equal(0.9999, result.Oee);         // ...but OEE doesn't inherit that rounding
    }
}
