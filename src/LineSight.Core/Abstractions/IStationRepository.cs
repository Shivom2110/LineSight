using LineSight.Core.Models;

namespace LineSight.Core.Abstractions;

public interface IStationRepository
{
    Task<IReadOnlyList<Station>> GetAllAsync(CancellationToken ct = default);

    /// <summary>Every station with its latest reported state.</summary>
    Task<IReadOnlyList<StationStatus>> GetStatusesAsync(CancellationToken ct = default);
}
