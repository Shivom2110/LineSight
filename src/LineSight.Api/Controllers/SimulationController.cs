using LineSight.Api.Simulation;
using LineSight.Core.Contracts;
using Microsoft.AspNetCore.Mvc;

namespace LineSight.Api.Controllers;

/// <summary>Start/stop the simulated line embedded in the API (the dashboard's "Start simulation" button).</summary>
[ApiController]
[Route("api/simulation")]
public sealed class SimulationController(SimulationRunner runner) : ControllerBase
{
    private const int MaxMinutes = 60;

    [HttpGet]
    public SimulationStatusResponse Get() => runner.Status;

    /// <summary>Run the line for <paramref name="minutes"/> (1-60, default 10). Calling it again extends the run.</summary>
    [HttpPost("start")]
    [ProducesResponseType<SimulationStatusResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public IActionResult Start([FromQuery] int minutes = 10)
    {
        if (minutes is < 1 or > MaxMinutes)
        {
            ModelState.AddModelError(nameof(minutes), $"minutes must be between 1 and {MaxMinutes}.");
            return ValidationProblem(ModelState);
        }

        return Ok(runner.Start(TimeSpan.FromMinutes(minutes)));
    }

    [HttpPost("stop")]
    public async Task<SimulationStatusResponse> Stop()
    {
        await runner.StopAsync();
        return runner.Status;
    }
}
