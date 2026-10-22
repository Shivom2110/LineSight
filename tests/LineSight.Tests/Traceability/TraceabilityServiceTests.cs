using LineSight.Core.Models;
using LineSight.Core.Traceability;
using LineSight.Tests.Fakes;

namespace LineSight.Tests.Traceability;

public class TraceabilityServiceTests
{
    private const string Serial = "FS-20261022-00001";
    private static readonly DateTime T0 = new(2026, 10, 22, 8, 0, 0, DateTimeKind.Utc);

    private readonly InMemoryModuleRepository _modules = new();
    private readonly TraceabilityService _service;

    public TraceabilityServiceTests()
    {
        _service = new TraceabilityService(new FakeStationRepository(), _modules);
    }

    private Task<StepOutcome> Record(Station station, StepResult result = StepResult.Pass, int minute = 0) =>
        _service.RecordStepAsync(Serial, station.Id, result, T0.AddMinutes(minute));

    [Fact]
    public async Task FirstStepAtDeposition_CreatesTheModule()
    {
        var outcome = await Record(TestLine.Deposition);

        Assert.True(outcome.IsAccepted);
        Assert.Equal(ModuleStatus.InProgress, outcome.NewStatus);
        Assert.Equal(ModuleStatus.InProgress, _modules.Modules[Serial].Status);
    }

    [Fact]
    public async Task AllStationsInOrder_AreAccepted_AndModuleEndsCompleted()
    {
        var minute = 0;
        foreach (var station in TestLine.Stations)
        {
            var outcome = await Record(station, minute: minute++);
            Assert.True(outcome.IsAccepted, $"{station.Name} was rejected: {outcome.Message}");
        }

        Assert.Equal(ModuleStatus.Completed, _modules.Modules[Serial].Status);
        Assert.Equal(5, _modules.Modules[Serial].Steps.Count);
    }

    [Fact]
    public async Task SkippingAStation_IsRejected()
    {
        await Record(TestLine.Deposition);

        var outcome = await Record(TestLine.Lamination, minute: 1);   // skipped Laser Scribe

        Assert.Equal(StepRejection.StationOutOfOrder, outcome.Rejection);
        Assert.Contains("Laser Scribe", outcome.Message);
    }

    [Fact]
    public async Task RepeatingTheSameStation_IsRejected()
    {
        await Record(TestLine.Deposition);
        await Record(TestLine.LaserScribe, minute: 1);

        var outcome = await Record(TestLine.LaserScribe, minute: 2);

        Assert.Equal(StepRejection.StationOutOfOrder, outcome.Rejection);
    }

    [Fact]
    public async Task DuplicateSerial_AtFirstStation_IsRejected()
    {
        await Record(TestLine.Deposition);

        var outcome = await Record(TestLine.Deposition, minute: 1);

        Assert.Equal(StepRejection.DuplicateSerial, outcome.Rejection);
    }

    [Fact]
    public async Task UnknownStation_IsRejected()
    {
        var outcome = await _service.RecordStepAsync(Serial, stationId: 999, StepResult.Pass, T0);

        Assert.Equal(StepRejection.UnknownStation, outcome.Rejection);
    }

    [Fact]
    public async Task ScrappedModule_CannotContinue()
    {
        await Record(TestLine.Deposition);
        var scrap = await Record(TestLine.LaserScribe, StepResult.Scrap, minute: 1);

        var next = await Record(TestLine.Lamination, minute: 2);

        Assert.Equal(ModuleStatus.Scrapped, scrap.NewStatus);
        Assert.Equal(StepRejection.ModuleScrapped, next.Rejection);
    }

    [Fact]
    public async Task CompletedModule_CannotTakeMoreSteps()
    {
        var minute = 0;
        foreach (var station in TestLine.Stations)
            await Record(station, minute: minute++);

        var outcome = await Record(TestLine.Pack, minute: minute);

        Assert.Equal(StepRejection.ModuleCompleted, outcome.Rejection);
    }

    [Theory]
    [InlineData("")]
    [InlineData("ABC-123")]
    [InlineData("FS-20261022-1")]
    public async Task InvalidSerialFormat_IsRejected(string badSerial)
    {
        var outcome = await _service.RecordStepAsync(badSerial, TestLine.Deposition.Id, StepResult.Pass, T0);

        Assert.Equal(StepRejection.InvalidSerial, outcome.Rejection);
    }

    [Fact]
    public async Task RejectedStep_IsNeverWrittenToTheRepository()
    {
        await Record(TestLine.Deposition);
        var writesBefore = _modules.WriteCount;

        await Record(TestLine.Pack, minute: 1);   // out of order

        Assert.Equal(writesBefore, _modules.WriteCount);
        Assert.Single(_modules.Modules[Serial].Steps);
    }

    [Fact]
    public async Task Genealogy_ReturnsStepsInStationOrder_EvenIfStoredOutOfOrder()
    {
        // Simulate a store that hands steps back in arbitrary order.
        var steps = new List<ModuleStep>
        {
            new(TestLine.Lamination.Id, "Lamination", 3, StepResult.Pass, T0.AddMinutes(2)),
            new(TestLine.Deposition.Id, "Deposition", 1, StepResult.Pass, T0),
            new(TestLine.LaserScribe.Id, "Laser Scribe", 2, StepResult.Pass, T0.AddMinutes(1)),
        };
        _modules.Modules[Serial] = new Module(Serial, ModuleStatus.InProgress, T0, steps);

        var module = await _service.GetGenealogyAsync(Serial);

        Assert.NotNull(module);
        Assert.Equal(["Deposition", "Laser Scribe", "Lamination"], module.Steps.Select(s => s.StationName));
    }

    [Fact]
    public async Task Genealogy_ForUnknownSerial_ReturnsNull()
    {
        Assert.Null(await _service.GetGenealogyAsync("FS-20261022-99999"));
    }
}
