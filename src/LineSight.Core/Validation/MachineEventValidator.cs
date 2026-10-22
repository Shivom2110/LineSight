using LineSight.Core.Models;

namespace LineSight.Core.Validation;

/// <summary>Business validation for machine events coming from the PLC layer.</summary>
public static class MachineEventValidator
{
    public static IReadOnlyList<ValidationError> Validate(MachineEvent machineEvent, DateTime utcNow) =>
        throw new NotImplementedException();
}
