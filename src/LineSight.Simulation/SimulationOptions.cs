namespace LineSight.Simulation;

/// <summary>
/// How the simulated line behaves. Used by both the console simulator (appsettings "Simulator" section)
/// and the simulator embedded in the API (appsettings "Simulation" section).
/// </summary>
public class SimulationOptions
{
    /// <summary>How often each station reports (the PLC scan/report rate).</summary>
    public int TickIntervalMs { get; set; } = 1000;

    /// <summary>Probability that a finished module is scrapped (0.03 = 3%).</summary>
    public double ScrapRate { get; set; } = 0.03;

    /// <summary>Per station, per tick probability of a breakdown starting.</summary>
    public double DownChancePerTick { get; set; } = 0.005;

    /// <summary>Max modules waiting in front of a station before the upstream station is blocked.</summary>
    public int BufferCapacity { get; set; } = 5;

    /// <summary>Fixed seed for a reproducible run; null = different every run.</summary>
    public int? Seed { get; set; }
}
