namespace LineSight.Core.Models;

/// <summary>Standard downtime reason codes used by the simulator and dashboard.</summary>
public static class DowntimeReasons
{
    public const string MaterialShortage = "Material Shortage";
    public const string EquipmentFault = "Equipment Fault";
    public const string Changeover = "Changeover";
    public const string QualityHold = "Quality Hold";

    public static IReadOnlyList<string> All { get; } =
        [MaterialShortage, EquipmentFault, Changeover, QualityHold];

    /// <summary>Max length matches the DowntimeReason column (NVARCHAR(100)).</summary>
    public const int MaxLength = 100;
}
