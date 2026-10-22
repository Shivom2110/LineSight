namespace LineSight.Core.Models;

/// <summary>A module in a list view: where it is now, without its full genealogy.</summary>
public sealed record ModuleSummary(
    string SerialNumber,
    ModuleStatus Status,
    DateTime CreatedUtc,
    string? LastStation,
    DateTime? LastStepUtc);
