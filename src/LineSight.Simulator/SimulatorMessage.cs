using LineSight.Core.Contracts;

namespace LineSight.Simulator;

/// <summary>Something the simulated PLC layer needs to send to the MES API.</summary>
public abstract record SimulatorMessage;

public sealed record MachineEventMessage(RecordMachineEventRequest Request) : SimulatorMessage;

public sealed record ModuleStepMessage(string SerialNumber, RecordModuleStepRequest Request) : SimulatorMessage;
