using LineSight.Core.Models;

namespace LineSight.Dashboard.ViewModels;

/// <summary>One line in the genealogy grid (timestamp converted to the operator's local time).</summary>
public sealed record GenealogyRow(int Sequence, string Station, StepResult Result, DateTime LocalTime);
