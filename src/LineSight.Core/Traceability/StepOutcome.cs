using LineSight.Core.Models;

namespace LineSight.Core.Traceability;

/// <summary>What happened when we tried to record a step; <see cref="NewStatus"/> is set when accepted.</summary>
public sealed record StepOutcome(StepRejection Rejection, string? Message, ModuleStatus? NewStatus)
{
    public bool IsAccepted => Rejection == StepRejection.None;

    public static StepOutcome Accepted(ModuleStatus newStatus) => new(StepRejection.None, null, newStatus);

    public static StepOutcome Rejected(RoutingDecision decision) => new(decision.Rejection, decision.Message, null);
}
