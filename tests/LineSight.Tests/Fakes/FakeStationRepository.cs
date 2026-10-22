using LineSight.Core.Abstractions;
using LineSight.Core.Models;

namespace LineSight.Tests.Fakes;

public sealed class FakeStationRepository(IReadOnlyList<Station>? stations = null) : IStationRepository
{
    private readonly IReadOnlyList<Station> _stations = stations ?? TestLine.Stations;

    public Task<IReadOnlyList<Station>> GetAllAsync(CancellationToken ct = default) => Task.FromResult(_stations);

    public Task<IReadOnlyList<StationStatus>> GetStatusesAsync(CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<StationStatus>>(
            _stations.Select(s => new StationStatus(s, StationState.Running, null, DateTime.UtcNow)).ToList());
}
