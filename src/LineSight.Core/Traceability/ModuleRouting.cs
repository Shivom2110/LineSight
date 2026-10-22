using LineSight.Core.Models;

namespace LineSight.Core.Traceability;

/// <summary>The routing rules for moving a module through the line.</summary>
public static class ModuleRouting
{
    public static RoutingDecision Validate(ProductionLine line, Module? existing, int stationId) =>
        throw new NotImplementedException();

    public static ModuleStatus StatusAfter(ProductionLine line, Station station, StepResult result) =>
        throw new NotImplementedException();
}
