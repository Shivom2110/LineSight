namespace LineSight.Core.Oee;

/// <summary>
/// Raw, aggregated numbers for one station over a time window. These come from the
/// usp_GetStationOEE stored procedure; the math on top of them lives in <see cref="OeeCalculator"/>.
/// </summary>
public sealed record OeeInput(
    double PlannedProductionTimeSeconds,
    double RunTimeSeconds,
    double IdealCycleTimeSeconds,
    int GoodCount,
    int ScrapCount)
{
    public int TotalCount => GoodCount + ScrapCount;
}
