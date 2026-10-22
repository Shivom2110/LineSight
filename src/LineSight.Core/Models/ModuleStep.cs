namespace LineSight.Core.Models;

/// <summary>One entry in a module's genealogy: it was processed at a station with a result.</summary>
public sealed record ModuleStep(
    int StationId,
    string StationName,
    int StationSequence,
    StepResult Result,
    DateTime TimestampUtc);
