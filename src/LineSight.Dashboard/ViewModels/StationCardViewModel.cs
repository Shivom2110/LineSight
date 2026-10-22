using CommunityToolkit.Mvvm.ComponentModel;
using LineSight.Core.Contracts;
using LineSight.Core.Models;

namespace LineSight.Dashboard.ViewModels;

/// <summary>
/// One station card. Instances live for the whole session and are updated in place on each
/// refresh, so bindings just see property changes (no flicker from re-creating the list).
/// </summary>
public sealed partial class StationCardViewModel(int id, string name) : ObservableObject
{
    public int Id { get; } = id;

    public string Name { get; } = name;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StateText))]
    private StationState? _state;

    [ObservableProperty] private string? _downtimeReason;
    [ObservableProperty] private double _oee;
    [ObservableProperty] private double _availability;
    [ObservableProperty] private double _performance;
    [ObservableProperty] private double _quality;
    [ObservableProperty] private int _goodCount;
    [ObservableProperty] private int _scrapCount;
    [ObservableProperty] private string? _warning;
    [ObservableProperty] private string? _topDowntime;

    public string StateText => State?.ToString() ?? "No data";

    public void Update(StationStatusResponse status, OeeResponse? oee)
    {
        State = status.CurrentState;
        DowntimeReason = status.DowntimeReason;

        if (oee is null)
            return;

        Oee = oee.Oee;
        Availability = oee.Availability;
        Performance = oee.Performance;
        Quality = oee.Quality;
        GoodCount = oee.GoodCount;
        ScrapCount = oee.ScrapCount;
        Warning = oee.Warning;

        var top = oee.Downtime.MaxBy(d => d.DurationSeconds);
        TopDowntime = top is null ? null : $"Top downtime: {top.Reason} ({top.DurationSeconds:0}s, {top.Occurrences}x)";
    }
}
