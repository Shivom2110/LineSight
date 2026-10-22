namespace LineSight.Core.Oee;

/// <summary>Pure OEE math: Availability x Performance x Quality.</summary>
public static class OeeCalculator
{
    public static double Availability(double runTimeSeconds, double plannedProductionTimeSeconds) =>
        throw new NotImplementedException();

    public static double Performance(double idealCycleTimeSeconds, int totalCount, double runTimeSeconds) =>
        throw new NotImplementedException();

    public static double Quality(int goodCount, int totalCount) =>
        throw new NotImplementedException();
}
