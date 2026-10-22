using LineSight.Core.Abstractions;
using LineSight.Core.Models;

namespace LineSight.Tests.Fakes;

public sealed class FakeMachineEventRepository : IMachineEventRepository
{
    public List<MachineEvent> Events { get; } = [];

    public Task<long> RecordAsync(MachineEvent machineEvent, CancellationToken ct = default)
    {
        Events.Add(machineEvent);
        return Task.FromResult((long)Events.Count);
    }
}
