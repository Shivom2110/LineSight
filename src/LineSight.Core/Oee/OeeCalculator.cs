namespace LineSight.Core.Oee;

/// <summary>
/// Pure OEE math: Availability x Performance x Quality.
/// Divide-by-zero cases (no planned time, no run time, no parts) return 0 rather than throwing,
/// because "nothing happened in this window" is a normal situation on a dashboard, not an error.
/// Negative inputs are programming/data errors and DO throw.
/// </summary>
public static class OeeCalculator
{
    /// <summary>Availability = RunTime / PlannedProductionTime, capped at 1.</summary>
    public static double Availability(double runTimeSeconds, double plannedProductionTimeSeconds)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(runTimeSeconds);
        ArgumentOutOfRangeException.ThrowIfNegative(plannedProductionTimeSeconds);

        if (plannedProductionTimeSeconds == 0)
            return 0;

        // Run time can never really exceed planned time; if it does, it's timestamp rounding.
        return Math.Min(1.0, runTimeSeconds / plannedProductionTimeSeconds);
    }

    /// <summary>
    /// Performance = (IdealCycleTime x TotalCount) / RunTime.
    /// Deliberately NOT capped at 1 - see <see cref="OeeResult.PerformanceAboveIdeal"/>.
    /// </summary>
    public static double Performance(double idealCycleTimeSeconds, int totalCount, double runTimeSeconds)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(idealCycleTimeSeconds);
        ArgumentOutOfRangeException.ThrowIfNegative(totalCount);
        ArgumentOutOfRangeException.ThrowIfNegative(runTimeSeconds);

        if (runTimeSeconds == 0)
            return 0;

        return idealCycleTimeSeconds * totalCount / runTimeSeconds;
    }

    /// <summary>Quality = GoodCount / TotalCount.</summary>
    public static double Quality(int goodCount, int totalCount)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(goodCount);
        ArgumentOutOfRangeException.ThrowIfNegative(totalCount);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(goodCount, totalCount);

        if (totalCount == 0)
            return 0;

        return (double)goodCount / totalCount;
    }
}
