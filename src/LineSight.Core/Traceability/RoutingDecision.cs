namespace LineSight.Core.Traceability;

/// <summary>Result of checking a proposed step against the routing rules.</summary>
public sealed record RoutingDecision(StepRejection Rejection, string? Message)
{
    public bool IsAllowed => Rejection == StepRejection.None;

    public static RoutingDecision Allowed { get; } = new(StepRejection.None, null);

    public static RoutingDecision Reject(StepRejection rejection, string message) => new(rejection, message);
}
