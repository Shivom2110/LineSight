using System.Net;
using LineSight.Core.Contracts;
using LineSight.Core.Models;
using LineSight.Simulation;
using LineSight.Simulator;

namespace LineSight.Tests.Simulator;

public class ApiSenderTests
{
    private static readonly SimulatorMessage Event = new MachineEventMessage(
        new RecordMachineEventRequest(1, StationState.Running, null, 1, 0, DateTime.UtcNow));

    /// <summary>Plays back a scripted list of responses (or exceptions) instead of using the network.</summary>
    private sealed class ScriptedHandler(params Func<HttpResponseMessage>[] script) : HttpMessageHandler
    {
        public List<string> Paths { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Paths.Add(request.RequestUri!.AbsolutePath);
            return Task.FromResult(script[Math.Min(Paths.Count - 1, script.Length - 1)]());
        }
    }

    private static ApiSender CreateSender(ScriptedHandler handler) =>
        new(new HttpClient(handler) { BaseAddress = new Uri("http://test") }, _ => { },
            TimeSpan.FromMilliseconds(1), TimeSpan.FromMilliseconds(5));

    [Fact]
    public async Task ApiUnavailable_IsRetriedUntilDelivered_SoTheEventIsNotLost()
    {
        var handler = new ScriptedHandler(
            () => throw new HttpRequestException("connection refused"),
            () => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable),
            () => new HttpResponseMessage(HttpStatusCode.Created));

        var delivered = await CreateSender(handler).SendAsync(Event, CancellationToken.None);

        Assert.True(delivered);
        Assert.Equal(3, handler.Paths.Count);
        Assert.All(handler.Paths, p => Assert.Equal("/api/events", p));
    }

    [Fact]
    public async Task ClientError_IsNotRetried()
    {
        // A 409 (e.g. routing conflict) will fail the same way every time - retrying would loop forever.
        var handler = new ScriptedHandler(() => new HttpResponseMessage(HttpStatusCode.Conflict));
        var step = new ModuleStepMessage("FS-20261022-00001", new RecordModuleStepRequest(3, StepResult.Pass));

        var delivered = await CreateSender(handler).SendAsync(step, CancellationToken.None);

        Assert.False(delivered);
        Assert.Equal(["/api/modules/FS-20261022-00001/steps"], handler.Paths);
    }
}
