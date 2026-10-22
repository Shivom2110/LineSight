namespace LineSight.Core.Models;

/// <summary>
/// A process station on the line. <see cref="Sequence"/> defines the routing order (1 = first).
/// <see cref="IdealCycleTimeSeconds"/> is the engineered "best possible" time per part, used for OEE Performance.
/// </summary>
public sealed record Station(int Id, string Name, int Sequence, double IdealCycleTimeSeconds);
