namespace LineSight.Core.Traceability;

/// <summary>Why a module step was refused (None = accepted).</summary>
public enum StepRejection
{
    None,
    InvalidSerial,
    UnknownStation,
    DuplicateSerial,
    StationOutOfOrder,
    ModuleScrapped,
    ModuleCompleted
}
