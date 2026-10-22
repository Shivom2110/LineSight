using LineSight.Core.Abstractions;
using LineSight.Core.Oee;

namespace LineSight.Tests.Fakes;

/// <summary>Returns the same canned aggregates for every known station; null for unknown ones.</summary>
public sealed class FakeOeeRepository : IOeeRepository
{
    // A = 0.9, P = 0.9444, Q = 0.9608, OEE = 0.8167 (same numbers as OeeCalculateTests).
    public static readonly OeeInput SampleInput = new(3600, 3240, 30, GoodCount: 98, ScrapCount: 4);

    public Task<StationOeeData?> GetStationOeeDataAsync(int stationId, DateTime fromUtc, DateTime toUtc,
        CancellationToken ct = default)
    {
        var station = TestLine.Stations.FirstOrDefault(s => s.Id == stationId);
        return Task.FromResult(station is null
            ? null
            : new StationOeeData(station.Name, SampleInput, [new DowntimeSummary("Equipment Fault", 360, 2)]));
    }
}
