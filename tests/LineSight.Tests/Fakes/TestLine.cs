using LineSight.Core.Models;

namespace LineSight.Tests.Fakes;

/// <summary>
/// The 5-station thin-film line used across tests. Ids are deliberately NOT 1..5 so tests prove
/// the code routes by <see cref="Station.Sequence"/>, not by Id.
/// </summary>
public static class TestLine
{
    public static readonly Station Deposition = new(10, "Deposition", 1, 2.0);
    public static readonly Station LaserScribe = new(20, "Laser Scribe", 2, 1.5);
    public static readonly Station Lamination = new(30, "Lamination", 3, 2.5);
    public static readonly Station ElectricalTest = new(40, "Electrical Test", 4, 1.0);
    public static readonly Station Pack = new(50, "Pack", 5, 1.2);

    public static IReadOnlyList<Station> Stations { get; } =
        [Deposition, LaserScribe, Lamination, ElectricalTest, Pack];

    public static ProductionLine Line => new(Stations);
}
