namespace LineSight.Core.Validation;

/// <summary>One problem with an input, tied to the field that caused it.</summary>
public sealed record ValidationError(string Field, string Message);
