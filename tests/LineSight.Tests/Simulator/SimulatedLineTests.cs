using LineSight.Core.Models;
using LineSight.Core.Traceability;
using LineSight.Simulator;
using LineSight.Tests.Fakes;

namespace LineSight.Tests.Simulator;

public class SimulatedLineTests
{
    private static List<SimulatorMessage> Run(int ticks, int seed = 42)
    {
        var line = new SimulatedLine(TestLine.Stations,
            new SimulatorOptions { ScrapRate = 0.05, DownChancePerTick = 0.01 }, new Random(seed));
        var start = new DateTime(2026, 10, 22, 8, 0, 0, DateTimeKind.Utc);

        return Enumerable.Range(0, ticks)
            .SelectMany(i => line.Tick(start.AddSeconds(i), TimeSpan.FromSeconds(1)))
            .ToList();
    }

    [Fact]
    public void EveryTick_ReportsOneEventPerStation()
    {
        var events = Run(ticks: 10).OfType<MachineEventMessage>().ToList();

        Assert.Equal(50, events.Count);
        Assert.Equal(10, events.Count(e => e.Request.StationId == TestLine.Pack.Id));
    }

    [Fact]
    public void GeneratedModuleSteps_AlwaysObeyTheRoutingRules()
    {
        // Replay everything the simulator produced through the real Core routing rules.
        var line = TestLine.Line;
        var modules = new Dictionary<string, Module>();

        foreach (var step in Run(ticks: 2000).OfType<ModuleStepMessage>())
        {
            modules.TryGetValue(step.SerialNumber, out var existing);
            var decision = ModuleRouting.Validate(line, existing, step.Request.StationId);
            Assert.True(decision.IsAllowed, decision.Message);

            var station = line.Find(step.Request.StationId)!;
            var status = ModuleRouting.StatusAfter(line, station, step.Request.Result);
            var newStep = new ModuleStep(station.Id, station.Name, station.Sequence, step.Request.Result, DateTime.UtcNow);
            modules[step.SerialNumber] = existing is null
                ? new Module(step.SerialNumber, status, DateTime.UtcNow, [newStep])
                : existing with { Status = status, Steps = [.. existing.Steps, newStep] };
        }

        Assert.Contains(modules.Values, m => m.Status == ModuleStatus.Completed);
        Assert.Contains(modules.Values, m => m.Status == ModuleStatus.Scrapped);
    }

    [Fact]
    public void Events_LookLikeARealLine()
    {
        var events = Run(ticks: 2000).OfType<MachineEventMessage>().Select(m => m.Request).ToList();

        // Mostly running, but some idle and some down - and Down always carries a valid reason.
        Assert.True(events.Count(e => e.State == StationState.Running) > events.Count / 2);
        Assert.Contains(events, e => e.State == StationState.Idle);
        Assert.Contains(events, e => e.State == StationState.Down);
        Assert.All(events.Where(e => e.State == StationState.Down),
            e => Assert.Contains(e.DowntimeReason, DowntimeReasons.All));
        Assert.All(events.Where(e => e.State != StationState.Down), e => Assert.Null(e.DowntimeReason));
    }

    [Fact]
    public void Serials_AreValidAndUnique()
    {
        var firstSteps = Run(ticks: 500).OfType<ModuleStepMessage>()
            .Where(m => m.Request.StationId == TestLine.Deposition.Id)
            .Select(m => m.SerialNumber)
            .ToList();

        Assert.NotEmpty(firstSteps);
        Assert.All(firstSteps, s => Assert.True(SerialNumber.IsValid(s), s));
        Assert.Equal(firstSteps.Count, firstSteps.Distinct().Count());
    }
}
