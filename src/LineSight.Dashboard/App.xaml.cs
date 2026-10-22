using System.Net.Http;
using System.Windows;
using LineSight.Dashboard.Services;
using LineSight.Dashboard.ViewModels;

namespace LineSight.Dashboard;

/// <summary>
/// Composition root: builds the object graph (HttpClient -> API client -> ViewModel -> Window).
/// API URL comes from the LINESIGHT_API_URL environment variable, defaulting to the local API.
/// </summary>
public partial class App : Application
{
    private static readonly TimeSpan RefreshInterval = TimeSpan.FromSeconds(3);

    private readonly CancellationTokenSource _shutdown = new();
    private HttpClient? _http;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var apiUrl = Environment.GetEnvironmentVariable("LINESIGHT_API_URL") ?? "http://localhost:5080";
        _http = new HttpClient
        {
            BaseAddress = new Uri(apiUrl.TrimEnd('/') + "/"),
            Timeout = TimeSpan.FromSeconds(5)
        };

        var viewModel = new MainViewModel(new LineSightApiClient(_http));
        MainWindow = new MainWindow { DataContext = viewModel };
        MainWindow.Show();

        // Runs on the UI thread's async context; awaits free the thread between refreshes.
        _ = viewModel.RunAutoRefreshAsync(RefreshInterval, _shutdown.Token);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _shutdown.Cancel();
        _http?.Dispose();
        base.OnExit(e);
    }
}
