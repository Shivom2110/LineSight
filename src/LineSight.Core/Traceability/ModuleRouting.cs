using LineSight.Core.Models;

namespace LineSight.Core.Traceability;

/// <summary>
/// The routing rules for moving a module through the line:
/// a module is born at the first station, must visit every station strictly in sequence,
/// and stops for good once it is scrapped or completed.
/// </summary>
public static class ModuleRouting
{
    /// <param name="existing">The module as currently stored, or null if the serial is new.</param>
    public static RoutingDecision Validate(ProductionLine line, Module? existing, int stationId)
    {
        var station = line.Find(stationId);
        if (station is null)
            return RoutingDecision.Reject(StepRejection.UnknownStation, $"Station {stationId} does not exist.");

        if (existing is null)
        {
            return station.Id == line.First.Id
                ? RoutingDecision.Allowed
                : RoutingDecision.Reject(StepRejection.StationOutOfOrder,
                    $"New modules must start at {line.First.Name}, not {station.Name}.");
        }

        // A known serial showing up at the first station again means two physical modules share a serial.
        if (station.Id == line.First.Id)
            return RoutingDecision.Reject(StepRejection.DuplicateSerial,
                $"Serial {existing.SerialNumber} already exists.");

        if (existing.Status == ModuleStatus.Scrapped)
            return RoutingDecision.Reject(StepRejection.ModuleScrapped,
                $"Module {existing.SerialNumber} was scrapped and cannot continue.");

        if (existing.Status == ModuleStatus.Completed)
            return RoutingDecision.Reject(StepRejection.ModuleCompleted,
                $"Module {existing.SerialNumber} has already completed the line.");

        var lastStep = existing.Steps.MaxBy(s => s.StationSequence);
        var lastStation = lastStep is null ? null : line.Find(lastStep.StationId);
        var expected = lastStation is null ? line.First : line.NextAfter(lastStation);

        if (expected is null || expected.Id != station.Id)
            return RoutingDecision.Reject(StepRejection.StationOutOfOrder,
                $"Module {existing.SerialNumber} must go to {expected?.Name ?? "no further station"} next, not {station.Name}.");

        return RoutingDecision.Allowed;
    }

    /// <summary>Module status after a step: scrap always wins, passing the last station completes it.</summary>
    public static ModuleStatus StatusAfter(ProductionLine line, Station station, StepResult result) =>
        result == StepResult.Scrap ? ModuleStatus.Scrapped
        : station.Id == line.Last.Id ? ModuleStatus.Completed
        : ModuleStatus.InProgress;
}
