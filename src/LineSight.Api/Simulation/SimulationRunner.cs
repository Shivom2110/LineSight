using LineSight.Core.Abstractions;
using LineSight.Core.Contracts;
using LineSight.Core.Models;
using LineSight.Core.Traceability;
using LineSight.Simulation;
using Microsoft.Extensions.Options;

namespace LineSight.Api.Simulation;

/// <summary>
/// Runs the simulated PLC line inside the API, so a hosted demo can be started with one button
/// instead of a separate console app. Writes go through the same repositories and the same
/// TraceabilityService routing rules as the HTTP endpoints.
///
/// It always stops itself after the requested duration, so a forgotten demo can't run up a
/// cloud database bill. Singleton: there is one line per API instance.
/// </summary>
public sealed class SimulationRunner(
    IServiceScopeFactory scopes,
    TimeProvider clock,
    IOptions<SimulationOptions> options,
    ILogger<SimulationRunner> logger) : IHostedService
{
    private readonly object _gate = new();
    private CancellationTokenSource? _cts;
    private Task? _loop;
    private DateTime? _startedUtc;
    private DateTime? _stopsAtUtc;
    private long _ticks;

    public SimulationStatusResponse Status
    {
        get
        {
            lock (_gate)
            {
                var running = _loop is { IsCompleted: false };
                return new SimulationStatusResponse(
                    running,
                    running ? _startedUtc : null,
                    running ? _stopsAtUtc : null,
                    Interlocked.Read(ref _ticks));
            }
        }
    }

    /// <summary>Starts the line, or - if it's already running - pushes the stop time out to now + duration.</summary>
    public SimulationStatusResponse Start(TimeSpan duration)
    {
        lock (_gate)
        {
            var now = clock.GetUtcNow().UtcDateTime;
            _stopsAtUtc = now + duration;

            if (_loop is not { IsCompleted: false })
            {
                _startedUtc = now;
                Interlocked.Exchange(ref _ticks, 0);
                _cts = new CancellationTokenSource();
                var token = _cts.Token;
                _loop = Task.Run(() => RunAsync(token), CancellationToken.None);
                logger.LogInformation("Simulation started; stops at {StopsAtUtc:u}", _stopsAtUtc);
            }

            return Status;
        }
    }

    public async Task StopAsync()
    {
        Task? loop;
        lock (_gate)
        {
            _cts?.Cancel();
            loop = _loop;
        }

        if (loop is not null)
            await loop;   // RunAsync never throws, so this just waits for it to finish
    }

    private async Task RunAsync(CancellationToken ct)
    {
        var settings = options.Value;
        var interval = TimeSpan.FromMilliseconds(settings.TickIntervalMs);

        try
        {
            SimulatedLine line;
            using (var scope = scopes.CreateScope())
            {
                var stations = await scope.ServiceProvider.GetRequiredService<IStationRepository>().GetAllAsync(ct);
                line = new SimulatedLine(stations, settings,
                    settings.Seed is { } seed ? new Random(seed) : new Random());
            }

            // Real-time timer for the pace of the ticks; the injected clock for timestamps and expiry.
            using var timer = new PeriodicTimer(interval);
            while (await timer.WaitForNextTickAsync(ct))
            {
                var now = clock.GetUtcNow().UtcDateTime;
                DateTime? stopsAt;
                lock (_gate) stopsAt = _stopsAtUtc;
                if (now >= stopsAt)
                    break;

                try
                {
                    await DeliverAsync(line.Tick(now, interval), ct);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    // e.g. database briefly unavailable: skip this tick, keep the line running.
                    logger.LogWarning(ex, "Simulation tick failed; continuing");
                }

                Interlocked.Increment(ref _ticks);
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // Stop was requested.
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Simulation could not start");
        }

        logger.LogInformation("Simulation stopped after {Ticks} ticks", Interlocked.Read(ref _ticks));
    }

    /// <summary>Applies one tick's PLC messages, in order, within a single DI scope.</summary>
    private async Task DeliverAsync(IReadOnlyList<SimulatorMessage> messages, CancellationToken ct)
    {
        using var scope = scopes.CreateScope();
        var events = scope.ServiceProvider.GetRequiredService<IMachineEventRepository>();
        var traceability = scope.ServiceProvider.GetRequiredService<TraceabilityService>();

        foreach (var message in messages)
        {
            switch (message)
            {
                case MachineEventMessage { Request: var e }:
                    await events.RecordAsync(new MachineEvent(
                        e.StationId, e.State, e.DowntimeReason, e.GoodCount, e.ScrapCount,
                        e.TimestampUtc ?? clock.GetUtcNow().UtcDateTime), ct);
                    break;

                case ModuleStepMessage { SerialNumber: var serial, Request: var step }:
                    var outcome = await traceability.RecordStepAsync(
                        serial, step.StationId, step.Result, step.TimestampUtc ?? clock.GetUtcNow().UtcDateTime, ct);
                    if (!outcome.IsAccepted)
                        logger.LogDebug("Simulated step rejected: {Message}", outcome.Message);
                    break;
            }
        }
    }

    Task IHostedService.StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <summary>App shutdown: stop the line cleanly.</summary>
    Task IHostedService.StopAsync(CancellationToken cancellationToken) => StopAsync();
}
