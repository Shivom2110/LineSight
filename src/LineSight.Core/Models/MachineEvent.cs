namespace LineSight.Core.Models;

/// <summary>
/// One status report from a station's PLC. Good/scrap counts are the parts finished
/// since the previous event (deltas, not running totals), so they can simply be summed.
/// </summary>
public sealed record MachineEvent(
    int StationId,
    StationState State,
    string? DowntimeReason,
    int GoodCount,
    int ScrapCount,
    DateTime TimestampUtc);
