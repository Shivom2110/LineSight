namespace LineSight.Core.Models;

/// <summary>A station plus its most recent reported state (null if it has never reported).</summary>
public sealed record StationStatus(
    Station Station,
    StationState? CurrentState,
    string? DowntimeReason,
    DateTime? LastEventUtc);
