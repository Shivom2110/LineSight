using LineSight.Core.Contracts;
using LineSight.Core.Models;
using LineSight.Core.Traceability;

namespace LineSight.Simulator;

/// <summary>
/// The simulated line (the "PLC layer"). Pure logic: no I/O, deterministic for a given Random seed.
///
/// Each station has an input buffer (WIP). On every tick a station is:
///  - Down    : a random breakdown with a downtime reason, lasting a few ticks
///  - Idle    : starved (empty buffer) or blocked (the next station's buffer is full)
///  - Running : works at 85-100% of its ideal speed, finishing modules as its progress passes 1.0
/// Finished modules are scrapped at <see cref="SimulatorOptions.ScrapRate"/>, otherwise passed downstream.
/// </summary>
public sealed class SimulatedLine
{
    private const double MinSpeed = 0.85;   // real machines rarely hit their ideal cycle time
    private const double MaxSpeed = 1.00;
    private const int MinDownTicks = 3;
    private const int MaxDownTicks = 15;

    private readonly List<StationSim> _stations;
    private readonly SimulatorOptions _options;
    private readonly Random _random;

    private DateOnly _serialDate;
    private int _nextSerial;

    public SimulatedLine(IReadOnlyList<Station> stations, SimulatorOptions options, Random random)
    {
        _stations = stations.OrderBy(s => s.Sequence).Select(s => new StationSim(s)).ToList();
        _options = options;
        _random = random;
    }

    /// <summary>Total modules currently waiting in buffers (work in progress).</summary>
    public int WorkInProgress => _stations.Sum(s => s.Buffer.Count);

    /// <summary>Advance the line by one tick and return the messages the PLCs would send.</summary>
    public IReadOnlyList<SimulatorMessage> Tick(DateTime nowUtc, TimeSpan elapsed)
    {
        var messages = new List<SimulatorMessage>();

        // Walk downstream -> upstream: downstream stations free buffer space first, and a module
        // finished this tick lands in a buffer that has already been processed, so it moves at
        // most one station per tick (its steps always go out in route order).
        for (var i = _stations.Count - 1; i >= 0; i--)
        {
            var next = i + 1 < _stations.Count ? _stations[i + 1] : null;
            TickStation(_stations[i], next, isFirst: i == 0, nowUtc, elapsed.TotalSeconds, messages);
        }

        return messages;
    }

    private void TickStation(StationSim sim, StationSim? next, bool isFirst, DateTime nowUtc, double seconds,
        List<SimulatorMessage> messages)
    {
        var good = 0;
        var scrap = 0;
        StationState state;
        string? reason = null;

        if (sim.DownTicksLeft == 0 && _random.NextDouble() < _options.DownChancePerTick)
        {
            sim.DownTicksLeft = _random.Next(MinDownTicks, MaxDownTicks + 1);
            sim.DownReason = DowntimeReasons.All[_random.Next(DowntimeReasons.All.Count)];
        }

        if (sim.DownTicksLeft > 0)
        {
            sim.DownTicksLeft--;
            state = StationState.Down;
            reason = sim.DownReason;
        }
        else if (!HasWork(sim, isFirst))
        {
            state = StationState.Idle;   // starved
            sim.Progress = 0;
        }
        else if (IsBlocked(next))
        {
            state = StationState.Idle;   // blocked - finished part waits, progress is kept
        }
        else
        {
            state = StationState.Running;
            var speed = MinSpeed + _random.NextDouble() * (MaxSpeed - MinSpeed);
            sim.Progress += seconds * speed / sim.Station.IdealCycleTimeSeconds;

            while (sim.Progress >= 1 && HasWork(sim, isFirst) && !IsBlocked(next))
            {
                sim.Progress -= 1;
                var serial = isFirst ? NextSerial(nowUtc) : sim.Buffer.Dequeue();
                var result = _random.NextDouble() < _options.ScrapRate ? StepResult.Scrap : StepResult.Pass;

                messages.Add(new ModuleStepMessage(serial, new RecordModuleStepRequest(sim.Station.Id, result, nowUtc)));

                if (result == StepResult.Pass)
                {
                    good++;
                    next?.Buffer.Enqueue(serial);   // after the last station the module is complete
                }
                else
                {
                    scrap++;
                }
            }
        }

        messages.Add(new MachineEventMessage(
            new RecordMachineEventRequest(sim.Station.Id, state, reason, good, scrap, nowUtc)));
    }

    // The first station always has raw substrate glass to work on.
    private static bool HasWork(StationSim sim, bool isFirst) => isFirst || sim.Buffer.Count > 0;

    private bool IsBlocked(StationSim? next) => next is not null && next.Buffer.Count >= _options.BufferCapacity;

    private string NextSerial(DateTime nowUtc)
    {
        var today = DateOnly.FromDateTime(nowUtc);
        if (today != _serialDate)
        {
            // Each day's counter starts at "seconds since midnight". The line makes fewer than one
            // module per second, so a restarted simulator always starts above any serial an earlier
            // run produced today - no duplicate serials across restarts, without asking the database.
            _serialDate = today;
            _nextSerial = (int)nowUtc.TimeOfDay.TotalSeconds + 1;
        }

        return SerialNumber.Create(today, _nextSerial++);
    }

    /// <summary>Mutable per-station simulation state.</summary>
    private sealed class StationSim(Station station)
    {
        public Station Station { get; } = station;
        public Queue<string> Buffer { get; } = new();
        public double Progress { get; set; }
        public int DownTicksLeft { get; set; }
        public string? DownReason { get; set; }
    }
}
