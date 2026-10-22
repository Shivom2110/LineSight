namespace LineSight.Api;

internal static class UtcTime
{
    /// <summary>
    /// Normalises an incoming timestamp to UTC. Values without an offset ("2026-10-22T08:00:00")
    /// are treated as UTC, since the whole system speaks UTC - never the server's local time zone.
    /// </summary>
    public static DateTime Normalize(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
    };
}
