using LineSight.Core.Oee;

namespace LineSight.Tests.Oee;

/// <summary>Each OEE factor tested on its own.</summary>
public class OeeFactorTests
{
    // ---------- Availability = RunTime / PlannedProductionTime ----------

    [Theory]
    [InlineData(3600, 3600, 1.0)]     // ran the whole shift
    [InlineData(2700, 3600, 0.75)]    // 15 min of downtime in an hour
    [InlineData(0, 3600, 0.0)]        // down the whole time
    public void Availability_IsRunTimeOverPlannedTime(double run, double planned, double expected)
    {
        Assert.Equal(expected, OeeCalculator.Availability(run, planned), precision: 4);
    }

    [Fact]
    public void Availability_WithZeroPlannedTime_ReturnsZeroInsteadOfThrowing()
    {
        Assert.Equal(0.0, OeeCalculator.Availability(runTimeSeconds: 0, plannedProductionTimeSeconds: 0));
    }

    [Fact]
    public void Availability_IsCappedAtOne_WhenRunTimeSlightlyExceedsPlanned()
    {
        // Timestamp rounding can make run time a hair larger than planned time.
        Assert.Equal(1.0, OeeCalculator.Availability(3601, 3600));
    }

    // ---------- Performance = (IdealCycleTime x TotalCount) / RunTime ----------

    [Theory]
    [InlineData(30, 100, 3000, 1.0)]  // exactly ideal speed
    [InlineData(30, 90, 3000, 0.9)]   // 10% slow
    [InlineData(2, 450, 1000, 0.9)]
    public void Performance_IsIdealTimeForCountOverRunTime(double ideal, int total, double run, double expected)
    {
        Assert.Equal(expected, OeeCalculator.Performance(ideal, total, run), precision: 4);
    }

    [Fact]
    public void Performance_WithZeroRunTime_ReturnsZeroInsteadOfThrowing()
    {
        Assert.Equal(0.0, OeeCalculator.Performance(idealCycleTimeSeconds: 30, totalCount: 10, runTimeSeconds: 0));
    }

    [Fact]
    public void Performance_AboveOneHundredPercent_IsNotCapped()
    {
        // 120 parts at 30s ideal = 3600s of "ideal" work done in 3000s -> 120%.
        Assert.Equal(1.2, OeeCalculator.Performance(30, 120, 3000), precision: 4);
    }

    // ---------- Quality = GoodCount / TotalCount ----------

    [Theory]
    [InlineData(100, 100, 1.0)]
    [InlineData(95, 100, 0.95)]
    [InlineData(0, 100, 0.0)]         // everything scrapped
    public void Quality_IsGoodOverTotal(int good, int total, double expected)
    {
        Assert.Equal(expected, OeeCalculator.Quality(good, total), precision: 4);
    }

    [Fact]
    public void Quality_WithZeroTotalCount_ReturnsZeroInsteadOfThrowing()
    {
        Assert.Equal(0.0, OeeCalculator.Quality(goodCount: 0, totalCount: 0));
    }

    [Fact]
    public void Factors_RejectNegativeInputs()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => OeeCalculator.Availability(-1, 3600));
        Assert.Throws<ArgumentOutOfRangeException>(() => OeeCalculator.Performance(30, -5, 3000));
        Assert.Throws<ArgumentOutOfRangeException>(() => OeeCalculator.Quality(-1, 10));
    }

    [Fact]
    public void Quality_RejectsMoreGoodPartsThanTotalParts()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => OeeCalculator.Quality(goodCount: 11, totalCount: 10));
    }
}
