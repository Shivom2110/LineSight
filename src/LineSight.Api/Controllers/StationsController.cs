using LineSight.Core.Abstractions;
using LineSight.Core.Contracts;
using Microsoft.AspNetCore.Mvc;

namespace LineSight.Api.Controllers;

[ApiController]
[Route("api/stations")]
public sealed class StationsController(IStationRepository stations) : ControllerBase
{
    /// <summary>All stations in line order with their latest reported state.</summary>
    [HttpGet]
    public async Task<IReadOnlyList<StationStatusResponse>> GetAll(CancellationToken ct)
    {
        var statuses = await stations.GetStatusesAsync(ct);

        return statuses
            .OrderBy(s => s.Station.Sequence)
            .Select(s => new StationStatusResponse(
                s.Station.Id, s.Station.Name, s.Station.Sequence, s.Station.IdealCycleTimeSeconds,
                s.CurrentState, s.DowntimeReason, s.LastEventUtc))
            .ToList();
    }
}
