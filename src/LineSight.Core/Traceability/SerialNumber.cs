namespace LineSight.Core.Traceability;

/// <summary>Module serial format: FS-yyyyMMdd-NNNNN, e.g. FS-20261022-00001.</summary>
public static class SerialNumber
{
    public static bool IsValid(string? serialNumber) => throw new NotImplementedException();

    public static string Create(DateOnly date, int sequence) => throw new NotImplementedException();
}
