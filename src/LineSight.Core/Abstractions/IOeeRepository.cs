using LineSight.Core.Oee;

namespace LineSight.Core.Abstractions;

public interface IOeeRepository
{
    /// <summary>Aggregated run time, counts and downtime for a station; null if the station doesn't exist.</summary>
    Task<StationOeeData?> GetStationOeeDataAsync(int stationId, DateTime fromUtc, DateTime toUtc, CancellationToken ct = default);
}
