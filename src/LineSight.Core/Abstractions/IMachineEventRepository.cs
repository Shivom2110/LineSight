using LineSight.Core.Models;

namespace LineSight.Core.Abstractions;

public interface IMachineEventRepository
{
    /// <summary>Stores the event and returns its new Id.</summary>
    Task<long> RecordAsync(MachineEvent machineEvent, CancellationToken ct = default);
}
