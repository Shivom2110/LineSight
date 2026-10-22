using LineSight.Core.Models;
using LineSight.Core.Oee;

namespace LineSight.Core.Contracts;

public sealed record RecordedEventResponse(long Id);

public sealed record RecordedStepResponse(string SerialNumber, ModuleStatus Status);

/// <summary>GET /api/stations item.</summary>
public sealed record StationStatusResponse(
    int Id,
    string Name,
    int Sequence,
    double IdealCycleTimeSeconds,
    StationState? CurrentState,
    string? DowntimeReason,
    DateTime? LastEventUtc);

/// <summary>GET /api/oee/{stationId}. Ratios are 0..1 (Performance may exceed 1, see Warning).</summary>
public sealed record OeeResponse(
    int StationId,
    string StationName,
    DateTime FromUtc,
    DateTime ToUtc,
    double PlannedProductionTimeSeconds,
    double RunTimeSeconds,
    int GoodCount,
    int ScrapCount,
    int TotalCount,
    double Availability,
    double Performance,
    double Quality,
    double Oee,
    bool PerformanceAboveIdeal,
    string? Warning,
    IReadOnlyList<DowntimeSummary> Downtime);

/// <summary>GET /api/modules/{serial}.</summary>
public sealed record ModuleGenealogyResponse(
    string SerialNumber,
    ModuleStatus Status,
    DateTime CreatedUtc,
    IReadOnlyList<ModuleStepResponse> Steps);

public sealed record ModuleStepResponse(
    int StationId,
    string StationName,
    int StationSequence,
    StepResult Result,
    DateTime TimestampUtc);
