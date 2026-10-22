using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LineSight.Dashboard.Services;

namespace LineSight.Dashboard.ViewModels;

/// <summary>
/// The whole dashboard's state and behaviour. Knows nothing about WPF controls - the XAML binds to it.
/// [ObservableProperty] generates INotifyPropertyChanged properties; [RelayCommand] generates ICommands.
/// </summary>
public sealed partial class MainViewModel(ILineSightApiClient api) : ObservableObject
{
    public static readonly TimeSpan OeeWindow = TimeSpan.FromMinutes(15);

    public ObservableCollection<StationCardViewModel> Stations { get; } = [];

    public ObservableCollection<GenealogyRow> GenealogySteps { get; } = [];

    public string OeeWindowText => $"Rolling OEE - last {OeeWindow.TotalMinutes:0} minutes";

    [ObservableProperty] private string _statusText = "Connecting to API...";
    [ObservableProperty] private bool _isConnected;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SearchCommand))]
    private string _searchSerial = "";

    [ObservableProperty] private string? _searchMessage;
    [ObservableProperty] private string? _moduleSummary;

    /// <summary>Loads station states and OEE. AsyncRelayCommand won't start a second refresh while one is running.</summary>
    [RelayCommand]
    private async Task RefreshAsync(CancellationToken ct)
    {
        try
        {
            var toUtc = DateTime.UtcNow;
            var fromUtc = toUtc - OeeWindow;

            var statuses = await api.GetStationsAsync(ct);
            // Fetch all stations' OEE in parallel rather than one after another.
            var oees = await Task.WhenAll(statuses.Select(s => api.GetOeeAsync(s.Id, fromUtc, toUtc, ct)));

            for (var i = 0; i < statuses.Count; i++)
            {
                var card = Stations.FirstOrDefault(c => c.Id == statuses[i].Id);
                if (card is null)
                {
                    card = new StationCardViewModel(statuses[i].Id, statuses[i].Name);
                    Stations.Add(card);
                }

                card.Update(statuses[i], oees[i]);
            }

            IsConnected = true;
            StatusText = $"Live - updated {DateTime.Now:HH:mm:ss}";
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            // Network down, timeout, bad response... keep showing the last known values and let
            // the next timer tick try again - a wall-mounted dashboard must never just stop.
            IsConnected = false;
            StatusText = $"API unreachable - retrying ({ex.Message})";
        }
    }

    [RelayCommand(CanExecute = nameof(CanSearch))]
    private async Task SearchAsync(CancellationToken ct)
    {
        var serial = SearchSerial.Trim();
        GenealogySteps.Clear();
        ModuleSummary = null;
        SearchMessage = null;

        try
        {
            var module = await api.GetModuleAsync(serial, ct);
            if (module is null)
            {
                SearchMessage = $"No module found with serial {serial}.";
                return;
            }

            ModuleSummary = $"{module.SerialNumber}  |  {module.Status}  |  started {module.CreatedUtc.ToLocalTime():yyyy-MM-dd HH:mm:ss}";
            foreach (var step in module.Steps)
                GenealogySteps.Add(new GenealogyRow(step.StationSequence, step.StationName, step.Result, step.TimestampUtc.ToLocalTime()));
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            SearchMessage = $"Could not reach the API ({ex.Message}).";
        }
    }

    private bool CanSearch() => !string.IsNullOrWhiteSpace(SearchSerial);

    /// <summary>Refreshes immediately, then every <paramref name="interval"/>, until cancelled.</summary>
    public async Task RunAutoRefreshAsync(TimeSpan interval, CancellationToken ct)
    {
        using var timer = new PeriodicTimer(interval);
        try
        {
            do
            {
                if (!RefreshCommand.IsRunning)
                    await RefreshCommand.ExecuteAsync(null);
            }
            while (await timer.WaitForNextTickAsync(ct));
        }
        catch (OperationCanceledException)
        {
            // App is shutting down.
        }
    }
}
