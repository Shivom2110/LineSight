using LineSight.Core.Models;
using LineSight.Core.Validation;

namespace LineSight.Tests.Validation;

public class MachineEventValidatorTests
{
    private static readonly DateTime Now = new(2026, 10, 22, 8, 0, 0, DateTimeKind.Utc);

    private static MachineEvent Event(
        StationState state = StationState.Running, string? reason = null,
        int good = 1, int scrap = 0, int stationId = 1, DateTime? at = null) =>
        new(stationId, state, reason, good, scrap, at ?? Now);

    private static IReadOnlyList<ValidationError> Validate(MachineEvent e) => MachineEventValidator.Validate(e, Now);

    [Fact]
    public void ValidRunningEvent_HasNoErrors()
    {
        Assert.Empty(Validate(Event()));
    }

    [Fact]
    public void DownWithReason_IsValid()
    {
        Assert.Empty(Validate(Event(StationState.Down, DowntimeReasons.EquipmentFault, good: 0)));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void DownWithoutReason_IsRejected(string? reason)
    {
        var errors = Validate(Event(StationState.Down, reason, good: 0));

        Assert.Contains(errors, e => e.Field == nameof(MachineEvent.DowntimeReason));
    }

    [Fact]
    public void ReasonOnANonDownState_IsRejected()
    {
        // Keeps downtime reporting clean: a reason only means something while the station is Down.
        var errors = Validate(Event(StationState.Idle, DowntimeReasons.Changeover, good: 0));

        Assert.Contains(errors, e => e.Field == nameof(MachineEvent.DowntimeReason));
    }

    [Fact]
    public void OverlongReason_IsRejected()
    {
        var errors = Validate(Event(StationState.Down, new string('x', DowntimeReasons.MaxLength + 1), good: 0));

        Assert.Contains(errors, e => e.Field == nameof(MachineEvent.DowntimeReason));
    }

    [Theory]
    [InlineData(-1, 0, nameof(MachineEvent.GoodCount))]
    [InlineData(0, -1, nameof(MachineEvent.ScrapCount))]
    public void NegativeCounts_AreRejected(int good, int scrap, string field)
    {
        Assert.Contains(Validate(Event(good: good, scrap: scrap)), e => e.Field == field);
    }

    [Fact]
    public void UndefinedState_IsRejected()
    {
        // e.g. JSON "state": 7 binds to an enum value that doesn't exist.
        Assert.Contains(Validate(Event(state: (StationState)7)), e => e.Field == nameof(MachineEvent.State));
    }

    [Fact]
    public void NonPositiveStationId_IsRejected()
    {
        Assert.Contains(Validate(Event(stationId: 0)), e => e.Field == nameof(MachineEvent.StationId));
    }

    [Fact]
    public void TimestampFarInTheFuture_IsRejected()
    {
        // PLC clocks drift a little, but an event an hour ahead would corrupt OEE windows.
        var errors = Validate(Event(at: Now.AddHours(1)));

        Assert.Contains(errors, e => e.Field == nameof(MachineEvent.TimestampUtc));
    }

    [Fact]
    public void MultipleProblems_AreAllReported()
    {
        var errors = Validate(Event(StationState.Down, reason: null, good: -1, scrap: -1));

        Assert.Equal(3, errors.Count);
    }
}
