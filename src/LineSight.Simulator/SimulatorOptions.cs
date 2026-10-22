namespace LineSight.Simulator;

/// <summary>Bound from the "Simulator" section of appsettings.json (or command-line overrides).</summary>
public sealed class SimulatorOptions
{
    public string ApiBaseUrl { get; set; } = "http://localhost:5080";

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
