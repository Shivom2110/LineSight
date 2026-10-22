using LineSight.Core.Models;

namespace LineSight.Simulator;

/// <summary>The simulated 5-station line. Pure logic: no I/O, deterministic for a given Random seed.</summary>
public sealed class SimulatedLine(IReadOnlyList<Station> stations, SimulatorOptions options, Random random)
{
    /// <summary>Advance the line by one tick and return the messages the PLCs would send.</summary>
    public IReadOnlyList<SimulatorMessage> Tick(DateTime nowUtc, TimeSpan elapsed) =>
        throw new NotImplementedException();
}
