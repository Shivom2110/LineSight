namespace LineSight.Data;

/// <summary>Small helpers shared by the repositories.</summary>
internal static class SqlTypes
{
    /// <summary>
    /// SQL Server's DATETIME2 has no time-zone info, so values come back as DateTimeKind.Unspecified.
    /// Everything we store is UTC, so stamp it as such (JSON then gets the trailing "Z").
    /// </summary>
    public static DateTime AsUtc(DateTime value) => DateTime.SpecifyKind(value, DateTimeKind.Utc);

    public static DateTime? AsUtc(DateTime? value) => value is null ? null : AsUtc(value.Value);

    public static TEnum ParseEnum<TEnum>(string value) where TEnum : struct, Enum => Enum.Parse<TEnum>(value);
}
