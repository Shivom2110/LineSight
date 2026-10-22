using System.Threading.Channels;

namespace LineSight.Simulator;

/// <summary>Sends simulator messages to the API in order, retrying with backoff until delivered.</summary>
public sealed class ApiSender(HttpClient http, Action<string> log, TimeSpan initialBackoff, TimeSpan maxBackoff)
{
    public Task RunAsync(ChannelReader<SimulatorMessage> reader, CancellationToken ct) =>
        throw new NotImplementedException();

    /// <summary>Returns true if delivered, false if the API permanently rejected it (4xx).</summary>
    public Task<bool> SendAsync(SimulatorMessage message, CancellationToken ct) =>
        throw new NotImplementedException();
}
