namespace LineSight.Core.Models;

/// <summary>A serialized solar module and its genealogy (the steps it has been through so far).</summary>
public sealed record Module(
    string SerialNumber,
    ModuleStatus Status,
    DateTime CreatedUtc,
    IReadOnlyList<ModuleStep> Steps);
