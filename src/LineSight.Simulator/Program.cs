using LineSight.Simulation;
using System.Threading.Channels;
using LineSight.Simulator;
using Microsoft.Extensions.Configuration;

// LineSight PLC simulator (ISA-95 Level 2): reports station state, part counts and module
// serials to the MES API. Override any setting on the command line, e.g.
//   dotnet run -- --Simulator:ApiBaseUrl=http://localhost:5080 --Simulator:TickIntervalMs=500

var configuration = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: true)
    .AddEnvironmentVariables(prefix: "LINESIGHT_")
    .AddCommandLine(args)
    .Build();

var options = configuration.GetSection("Simulator").Get<SimulatorOptions>() ?? new SimulatorOptions();
var tick = TimeSpan.FromMilliseconds(options.TickIntervalMs);

static void Log(string message) => Console.WriteLine($"{DateTime.Now:HH:mm:ss} {message}");

using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;   // let us shut down cleanly instead of killing the process
    cts.Cancel();
};

// Trailing slash so relative paths like "api/events" resolve under the base URL.
using var http = new HttpClient
{
    BaseAddress = new Uri(options.ApiBaseUrl.TrimEnd('/') + "/"),
    Timeout = TimeSpan.FromSeconds(5)
};
var sender = new ApiSender(http, Log, initialBackoff: TimeSpan.FromSeconds(1), maxBackoff: TimeSpan.FromSeconds(30));

try
{
    Log($"Connecting to {http.BaseAddress} ...");
    var stations = await sender.GetStationsAsync(cts.Token);
    Log($"Line: {string.Join(" -> ", stations.Select(s => s.Name))}. Tick = {tick.TotalMilliseconds} ms. Ctrl+C to stop.");

    var line = new SimulatedLine(stations, options, options.Seed is { } seed ? new Random(seed) : new Random());

    // The tick loop only produces messages; a separate task delivers them. If the API goes down
    // the queue grows instead of the simulation stalling, and nothing is dropped.
    var queue = Channel.CreateUnbounded<SimulatorMessage>();
    var sending = sender.RunAsync(queue.Reader, cts.Token);

    using var timer = new PeriodicTimer(tick);
    var ticks = 0;
    while (await timer.WaitForNextTickAsync(cts.Token))
    {
        foreach (var message in line.Tick(DateTime.UtcNow, tick))
            queue.Writer.TryWrite(message);   // unbounded channel: always succeeds

        if (++ticks % 10 == 0)
            Log($"tick {ticks}: WIP in buffers = {line.WorkInProgress}, messages waiting to send = {queue.Reader.Count}");
    }

    await sending;
}
catch (OperationCanceledException) when (cts.IsCancellationRequested)
{
    Log("Simulator stopped.");
}
