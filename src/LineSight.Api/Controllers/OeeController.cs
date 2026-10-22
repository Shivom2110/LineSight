using LineSight.Core.Abstractions;
using LineSight.Core.Contracts;
using LineSight.Core.Oee;
using Microsoft.AspNetCore.Mvc;

namespace LineSight.Api.Controllers;

[ApiController]
[Route("api/oee")]
public sealed class OeeController(IOeeRepository oee, TimeProvider clock) : ControllerBase
{
    private static readonly TimeSpan DefaultWindow = TimeSpan.FromHours(1);
    private static readonly TimeSpan MaxWindow = TimeSpan.FromDays(31);

    /// <summary>
    /// OEE breakdown for one station. Defaults to the last hour if from/to are omitted.
    /// SQL aggregates the events; the A x P x Q math is done by LineSight.Core.OeeCalculator.
    /// </summary>
    [HttpGet("{stationId:int}")]
    [ProducesResponseType<OeeResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(int stationId, [FromQuery] DateTime? from, [FromQuery] DateTime? to, CancellationToken ct)
    {
        var toUtc = to is { } t ? UtcTime.Normalize(t) : clock.GetUtcNow().UtcDateTime;
        var fromUtc = from is { } f ? UtcTime.Normalize(f) : toUtc - DefaultWindow;

        if (fromUtc >= toUtc)
            ModelState.AddModelError("from", "'from' must be earlier than 'to'.");
        else if (toUtc - fromUtc > MaxWindow)
            ModelState.AddModelError("from", $"The window can be at most {MaxWindow.TotalDays} days.");

        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        var data = await oee.GetStationOeeDataAsync(stationId, fromUtc, toUtc, ct);
        if (data is null)
            return Problem($"Station {stationId} was not found.", statusCode: StatusCodes.Status404NotFound);

        var result = OeeCalculator.Calculate(data.Input);

        return Ok(new OeeResponse(
            stationId,
            data.StationName,
            fromUtc,
            toUtc,
            data.Input.PlannedProductionTimeSeconds,
            data.Input.RunTimeSeconds,
            data.Input.GoodCount,
            data.Input.ScrapCount,
            data.Input.TotalCount,
            result.Availability,
            result.Performance,
            result.Quality,
            result.Oee,
            result.PerformanceAboveIdeal,
            result.PerformanceAboveIdeal
                ? "Performance is above 100% - check this station's ideal cycle time."
                : null,
            data.Downtime));
    }
}
