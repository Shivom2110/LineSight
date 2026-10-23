using LineSight.Core.Abstractions;
using LineSight.Core.Models;

namespace LineSight.Tests.Fakes;

/// <summary>Stands in for SQL Server: keeps modules in a dictionary (locked, since the embedded simulator writes from a background task).</summary>
public sealed class InMemoryModuleRepository(IReadOnlyList<Station>? stations = null) : IModuleRepository
{
    private readonly IReadOnlyList<Station> _stations = stations ?? TestLine.Stations;

    public Dictionary<string, Module> Modules { get; } = new();

    public int WriteCount { get; private set; }

    public Task<Module?> GetBySerialAsync(string serialNumber, CancellationToken ct = default)
    {
        lock (Modules) return Task.FromResult(Modules.GetValueOrDefault(serialNumber));
    }

    public Task<IReadOnlyList<ModuleSummary>> GetRecentAsync(int take, CancellationToken ct = default)
    {
        lock (Modules) return Task.FromResult<IReadOnlyList<ModuleSummary>>(Modules.Values
            .OrderByDescending(m => m.CreatedUtc)
            .Take(take)
            .Select(m => new ModuleSummary(m.SerialNumber, m.Status, m.CreatedUtc,
                m.Steps.MaxBy(s => s.StationSequence)?.StationName,
                m.Steps.MaxBy(s => s.StationSequence)?.TimestampUtc))
            .ToList());
    }

    public Task RecordStepAsync(string serialNumber, int stationId, StepResult result, DateTime timestampUtc,
        ModuleStatus newStatus, CancellationToken ct = default)
    {
        lock (Modules)
        {
            WriteCount++;
            var station = _stations.Single(s => s.Id == stationId);
            var step = new ModuleStep(station.Id, station.Name, station.Sequence, result, timestampUtc);

            Modules[serialNumber] = Modules.TryGetValue(serialNumber, out var existing)
                ? existing with { Status = newStatus, Steps = [.. existing.Steps, step] }
                : new Module(serialNumber, newStatus, timestampUtc, [step]);
        }

        return Task.CompletedTask;
    }
}
