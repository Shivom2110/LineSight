using LineSight.Core.Models;

namespace LineSight.Core.Abstractions;

public interface IModuleRepository
{
    /// <summary>The module and all its steps, or null if the serial has never been seen.</summary>
    Task<Module?> GetBySerialAsync(string serialNumber, CancellationToken ct = default);

    /// <summary>
    /// Persists one step and the module's new status. Creates the module if this is its first step.
    /// Callers must validate routing first (see <see cref="Traceability.TraceabilityService"/>).
    /// </summary>
    Task RecordStepAsync(
        string serialNumber,
        int stationId,
        StepResult result,
        DateTime timestampUtc,
        ModuleStatus newStatus,
        CancellationToken ct = default);
}
