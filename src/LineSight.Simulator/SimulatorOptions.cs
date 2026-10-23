using LineSight.Simulation;

namespace LineSight.Simulator;

/// <summary>Console simulator settings: the shared line behaviour plus where to send the data.</summary>
public sealed class SimulatorOptions : SimulationOptions
{
    public string ApiBaseUrl { get; set; } = "http://localhost:5080";
}
