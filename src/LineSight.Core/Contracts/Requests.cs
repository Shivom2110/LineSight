using LineSight.Core.Models;

namespace LineSight.Core.Contracts;

// Request bodies for the Web API. Shared by the API, the simulator and the dashboard so all
// three agree on the wire format without copy-pasted classes.

/// <summary>POST /api/events. TimestampUtc defaults to "now" on the server if omitted.</summary>
public sealed record RecordMachineEventRequest(
    int StationId,
    StationState State,
    string? DowntimeReason,
    int GoodCount,
    int ScrapCount,
    DateTime? TimestampUtc = null);

/// <summary>POST /api/modules/{serial}/steps.</summary>
public sealed record RecordModuleStepRequest(
    int StationId,
    StepResult Result,
    DateTime? TimestampUtc = null);
