using System.Globalization;
using System.Text.RegularExpressions;

namespace LineSight.Core.Traceability;

/// <summary>Module serial format: FS-yyyyMMdd-NNNNN, e.g. FS-20261022-00001.</summary>
public static partial class SerialNumber
{
    public const int MaxSequence = 99_999;

    [GeneratedRegex(@"^FS-(\d{8})-\d{5}$")]
    private static partial Regex Pattern();

    public static bool IsValid(string? serialNumber)
    {
        if (string.IsNullOrEmpty(serialNumber))
            return false;

        var match = Pattern().Match(serialNumber);

        // The regex checks the shape; the date part must also be a real calendar date.
        return match.Success && DateOnly.TryParseExact(
            match.Groups[1].Value, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _);
    }

    public static string Create(DateOnly date, int sequence)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(sequence);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(sequence, MaxSequence);

        return $"FS-{date:yyyyMMdd}-{sequence:D5}";
    }
}
