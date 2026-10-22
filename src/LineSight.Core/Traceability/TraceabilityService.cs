using LineSight.Core.Abstractions;
using LineSight.Core.Models;

namespace LineSight.Core.Traceability;

/// <summary>
/// Records module steps (enforcing <see cref="ModuleRouting"/>) and returns module genealogy.
/// Pure orchestration: rules live in ModuleRouting, storage behind the repository interfaces.
/// </summary>
public sealed class TraceabilityService(IStationRepository stations, IModuleRepository modules)
{
    public async Task<StepOutcome> RecordStepAsync(
        string serialNumber, int stationId, StepResult result, DateTime timestampUtc, CancellationToken ct = default)
    {
        if (!SerialNumber.IsValid(serialNumber))
            return StepOutcome.Rejected(RoutingDecision.Reject(StepRejection.InvalidSerial,
                $"'{serialNumber}' is not a valid serial (expected FS-yyyyMMdd-NNNNN)."));

        var line = new ProductionLine(await stations.GetAllAsync(ct));
        var existing = await modules.GetBySerialAsync(serialNumber, ct);

        var decision = ModuleRouting.Validate(line, existing, stationId);
        if (!decision.IsAllowed)
            return StepOutcome.Rejected(decision);

        var newStatus = ModuleRouting.StatusAfter(line, line.Find(stationId)!, result);
        await modules.RecordStepAsync(serialNumber, stationId, result, timestampUtc, newStatus, ct);

        return StepOutcome.Accepted(newStatus);
    }

    /// <summary>The module with its steps in line order, or null if the serial is unknown.</summary>
    public async Task<Module?> GetGenealogyAsync(string serialNumber, CancellationToken ct = default)
    {
        var module = await modules.GetBySerialAsync(serialNumber, ct);
        if (module is null)
            return null;

        var ordered = module.Steps
            .OrderBy(s => s.StationSequence)
            .ThenBy(s => s.TimestampUtc)
            .ToList();

        return module with { Steps = ordered };
    }
}
