using LineSight.Core.Abstractions;
using LineSight.Core.Models;

namespace LineSight.Tests.Fakes;

/// <summary>Captures recorded events. Thread-safe because the embedded simulator writes from a background task.</summary>
public sealed class FakeMachineEventRepository : IMachineEventRepository
{
    private readonly List<MachineEvent> _events = [];

    public IReadOnlyList<MachineEvent> Events
    {
        get { lock (_events) return _events.ToList(); }
    }

    public Task<long> RecordAsync(MachineEvent machineEvent, CancellationToken ct = default)
    {
        lock (_events)
        {
            _events.Add(machineEvent);
            return Task.FromResult((long)_events.Count);
        }
    }
}
