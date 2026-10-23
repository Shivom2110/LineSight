using System.Net;
using System.Net.Http.Json;
using LineSight.Core.Contracts;
using Microsoft.Extensions.Time.Testing;

namespace LineSight.Tests.Api;

/// <summary>
/// The simulator embedded in the API ("Start simulation" button). Ticks run on a real (fast) timer;
/// the business clock is a FakeTimeProvider so the auto-stop can be tested without waiting minutes.
/// </summary>
public sealed class SimulationApiTests : IDisposable
{
    private static readonly TimeSpan WaitLimit = TimeSpan.FromSeconds(15);

    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2026, 10, 22, 8, 0, 0, TimeSpan.Zero));
    private readonly LineSightApiFactory _factory;
    private readonly HttpClient _client;

    public SimulationApiTests()
    {
        _factory = new LineSightApiFactory
        {
            Clock = _clock,
            Settings = { ["Simulation:TickIntervalMs"] = "10", ["Simulation:Seed"] = "7" }
        };
        _client = _factory.CreateClient();
    }

    public void Dispose() => _factory.Dispose();

    private async Task<SimulationStatusResponse> Status() =>
        (await _client.GetFromJsonAsync<SimulationStatusResponse>("/api/simulation", LineSightJson.Options))!;

    private Task<HttpResponseMessage> Start(int minutes = 5) => _client.PostAsync($"/api/simulation/start?minutes={minutes}", null);

    private static async Task WaitUntil(Func<Task<bool>> condition)
    {
        var deadline = DateTime.UtcNow + WaitLimit;
        while (!await condition())
        {
            if (DateTime.UtcNow > deadline)
                throw new TimeoutException("Condition was not met in time.");
            await Task.Delay(20);
        }
    }

    [Fact]
    public async Task Status_BeforeStarting_IsNotRunning()
    {
        var status = await Status();

        Assert.False(status.Running);
        Assert.Null(status.StopsAtUtc);
    }

    [Fact]
    public async Task Start_RunsTheLine_RecordingEventsAndModules()
    {
        var response = await Start(minutes: 5);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var status = await response.Content.ReadFromJsonAsync<SimulationStatusResponse>(LineSightJson.Options);
        Assert.True(status!.Running);
        Assert.Equal(_clock.GetUtcNow().UtcDateTime.AddMinutes(5), status.StopsAtUtc);

        // Events are written through the same repositories the HTTP endpoints use...
        await WaitUntil(() => Task.FromResult(_factory.Events.Events.Count >= 25));
        // ...and modules go through the real TraceabilityService routing rules.
        await WaitUntil(() => Task.FromResult(_factory.Modules.Modules.Count > 0));
    }

    [Fact]
    public async Task Stop_StopsProducingData()
    {
        await Start();
        await WaitUntil(() => Task.FromResult(_factory.Events.Events.Count > 0));

        var response = await _client.PostAsync("/api/simulation/stop", null);
        var countAfterStop = _factory.Events.Events.Count;
        await Task.Delay(200);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False((await Status()).Running);
        Assert.Equal(countAfterStop, _factory.Events.Events.Count);
    }

    [Fact]
    public async Task Simulation_StopsByItself_WhenTheDurationIsUp()
    {
        await Start(minutes: 1);
        await WaitUntil(() => Task.FromResult(_factory.Events.Events.Count > 0));

        _clock.Advance(TimeSpan.FromSeconds(61));

        await WaitUntil(async () => !(await Status()).Running);
    }

    [Fact]
    public async Task StartingAgain_WhileRunning_ExtendsTheRun()
    {
        await Start(minutes: 1);
        _clock.Advance(TimeSpan.FromSeconds(30));

        var status = await (await Start(minutes: 5)).Content.ReadFromJsonAsync<SimulationStatusResponse>(LineSightJson.Options);

        Assert.True(status!.Running);
        Assert.Equal(_clock.GetUtcNow().UtcDateTime.AddMinutes(5), status.StopsAtUtc);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(61)]
    public async Task Start_WithDurationOutOfRange_Returns400(int minutes)
    {
        var response = await Start(minutes);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.False((await Status()).Running);
    }
}
