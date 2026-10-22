using LineSight.Core.Models;

namespace LineSight.Core.Validation;

/// <summary>
/// Business validation for machine events coming from the PLC layer.
/// Returns every problem at once (not just the first) so a client can fix its payload in one go.
/// </summary>
public static class MachineEventValidator
{
    /// <summary>How far ahead of server time an event may be stamped (PLC clock drift allowance).</summary>
    public static readonly TimeSpan MaxClockSkew = TimeSpan.FromMinutes(5);

    public static IReadOnlyList<ValidationError> Validate(MachineEvent machineEvent, DateTime utcNow)
    {
        ArgumentNullException.ThrowIfNull(machineEvent);
        var errors = new List<ValidationError>();

        if (machineEvent.StationId <= 0)
            errors.Add(new(nameof(MachineEvent.StationId), "StationId must be a positive number."));

        if (!Enum.IsDefined(machineEvent.State))
            errors.Add(new(nameof(MachineEvent.State), $"'{machineEvent.State}' is not a valid station state."));

        var hasReason = !string.IsNullOrWhiteSpace(machineEvent.DowntimeReason);
        if (machineEvent.State == StationState.Down && !hasReason)
            errors.Add(new(nameof(MachineEvent.DowntimeReason), "A downtime reason is required when the state is Down."));
        else if (machineEvent.State != StationState.Down && hasReason)
            errors.Add(new(nameof(MachineEvent.DowntimeReason), "A downtime reason is only allowed when the state is Down."));
        else if (machineEvent.DowntimeReason?.Length > DowntimeReasons.MaxLength)
            errors.Add(new(nameof(MachineEvent.DowntimeReason),
                $"Downtime reason must be at most {DowntimeReasons.MaxLength} characters."));

        if (machineEvent.GoodCount < 0)
            errors.Add(new(nameof(MachineEvent.GoodCount), "GoodCount cannot be negative."));

        if (machineEvent.ScrapCount < 0)
            errors.Add(new(nameof(MachineEvent.ScrapCount), "ScrapCount cannot be negative."));

        if (machineEvent.TimestampUtc > utcNow + MaxClockSkew)
            errors.Add(new(nameof(MachineEvent.TimestampUtc), "Timestamp is too far in the future."));

        return errors;
    }
}
