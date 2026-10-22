using LineSight.Core.Abstractions;
using LineSight.Core.Contracts;
using LineSight.Core.Models;
using LineSight.Core.Validation;
using Microsoft.AspNetCore.Mvc;

namespace LineSight.Api.Controllers;

/// <summary>Machine state reports from the PLC layer.</summary>
[ApiController]
[Route("api/events")]
public sealed class EventsController(
    IMachineEventRepository events,
    IStationRepository stations,
    TimeProvider clock) : ControllerBase
{
    /// <summary>Record one machine event (state + good/scrap counts since the last event).</summary>
    [HttpPost]
    [ProducesResponseType<RecordedEventResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Record(RecordMachineEventRequest request, CancellationToken ct)
    {
        var now = clock.GetUtcNow().UtcDateTime;

        var machineEvent = new MachineEvent(
            request.StationId,
            request.State,
            // Treat "" / "   " as "no reason" so the validator and the DB CHECK constraint agree.
            string.IsNullOrWhiteSpace(request.DowntimeReason) ? null : request.DowntimeReason.Trim(),
            request.GoodCount,
            request.ScrapCount,
            request.TimestampUtc is { } ts ? UtcTime.Normalize(ts) : now);

        foreach (var error in MachineEventValidator.Validate(machineEvent, now))
            ModelState.AddModelError(error.Field, error.Message);

        if (ModelState.IsValid && (await stations.GetAllAsync(ct)).All(s => s.Id != request.StationId))
            ModelState.AddModelError(nameof(request.StationId), $"Station {request.StationId} does not exist.");

        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        var id = await events.RecordAsync(machineEvent, ct);
        return StatusCode(StatusCodes.Status201Created, new RecordedEventResponse(id));
    }
}
