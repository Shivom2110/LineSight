using LineSight.Core.Contracts;
using LineSight.Core.Traceability;
using Microsoft.AspNetCore.Mvc;

namespace LineSight.Api.Controllers;

/// <summary>Serialized module tracking (WIP + genealogy).</summary>
[ApiController]
[Route("api/modules")]
public sealed class ModulesController(TraceabilityService traceability, TimeProvider clock) : ControllerBase
{
    /// <summary>Record a module passing or being scrapped at a station. Station order is enforced.</summary>
    [HttpPost("{serial}/steps")]
    [ProducesResponseType<RecordedStepResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> RecordStep(string serial, RecordModuleStepRequest request, CancellationToken ct)
    {
        if (!Enum.IsDefined(request.Result))
        {
            ModelState.AddModelError(nameof(request.Result), "Result must be Pass or Scrap.");
            return ValidationProblem(ModelState);
        }

        var timestamp = request.TimestampUtc is { } ts ? UtcTime.Normalize(ts) : clock.GetUtcNow().UtcDateTime;
        var outcome = await traceability.RecordStepAsync(serial, request.StationId, request.Result, timestamp, ct);

        return outcome.Rejection switch
        {
            StepRejection.None => Ok(new RecordedStepResponse(serial, outcome.NewStatus!.Value)),

            // The request itself is wrong -> 400.
            StepRejection.InvalidSerial or StepRejection.UnknownStation =>
                Problem(outcome.Message, statusCode: StatusCodes.Status400BadRequest, title: outcome.Rejection.ToString()),

            // The request is well-formed but conflicts with the module's current state -> 409.
            _ => Problem(outcome.Message, statusCode: StatusCodes.Status409Conflict, title: outcome.Rejection.ToString())
        };
    }

    /// <summary>Full genealogy of a module: every station it passed through, in order.</summary>
    [HttpGet("{serial}")]
    [ProducesResponseType<ModuleGenealogyResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetGenealogy(string serial, CancellationToken ct)
    {
        var module = await traceability.GetGenealogyAsync(serial, ct);
        if (module is null)
            return Problem($"Module {serial} was not found.", statusCode: StatusCodes.Status404NotFound);

        return Ok(new ModuleGenealogyResponse(
            module.SerialNumber,
            module.Status,
            module.CreatedUtc,
            module.Steps.Select(s => new ModuleStepResponse(
                s.StationId, s.StationName, s.StationSequence, s.Result, s.TimestampUtc)).ToList()));
    }
}
