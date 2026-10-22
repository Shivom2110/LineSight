using LineSight.Core.Models;
using LineSight.Core.Traceability;
using LineSight.Tests.Fakes;

namespace LineSight.Tests.Traceability;

/// <summary>Pure rule tests - no repository involved.</summary>
public class ModuleRoutingTests
{
    [Fact]
    public void ProductionLine_OrdersStationsBySequence_RegardlessOfInputOrder()
    {
        var line = new ProductionLine([TestLine.Pack, TestLine.Deposition, TestLine.Lamination,
            TestLine.ElectricalTest, TestLine.LaserScribe]);

        Assert.Equal(["Deposition", "Laser Scribe", "Lamination", "Electrical Test", "Pack"],
            line.Stations.Select(s => s.Name));
        Assert.Null(line.NextAfter(TestLine.Pack));
    }

    [Theory]
    [InlineData(StepResult.Pass, 20, ModuleStatus.InProgress)]   // passed a middle station
    [InlineData(StepResult.Pass, 50, ModuleStatus.Completed)]    // passed the last station
    [InlineData(StepResult.Scrap, 20, ModuleStatus.Scrapped)]
    [InlineData(StepResult.Scrap, 50, ModuleStatus.Scrapped)]    // scrap at Pack is still scrap
    public void StatusAfter_DependsOnResultAndWhetherStationIsLast(StepResult result, int stationId, ModuleStatus expected)
    {
        var line = TestLine.Line;

        Assert.Equal(expected, ModuleRouting.StatusAfter(line, line.Find(stationId)!, result));
    }

    [Fact]
    public void Validate_NewModule_MustStartAtFirstStation()
    {
        var decision = ModuleRouting.Validate(TestLine.Line, existing: null, TestLine.LaserScribe.Id);

        Assert.Equal(StepRejection.StationOutOfOrder, decision.Rejection);
        Assert.Contains("Deposition", decision.Message);
    }
}
