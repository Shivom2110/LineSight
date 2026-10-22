using LineSight.Core.Abstractions;
using LineSight.Core.Models;

namespace LineSight.Core.Traceability;

/// <summary>Records module steps (enforcing routing) and returns module genealogy.</summary>
public sealed class TraceabilityService(IStationRepository stations, IModuleRepository modules)
{
    public Task<StepOutcome> RecordStepAsync(
        string serialNumber, int stationId, StepResult result, DateTime timestampUtc, CancellationToken ct = default) =>
        throw new NotImplementedException();

    public Task<Module?> GetGenealogyAsync(string serialNumber, CancellationToken ct = default) =>
        throw new NotImplementedException();
}
