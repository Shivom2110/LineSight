using System.Net;
using System.Net.Http.Json;
using System.Threading.Channels;
using LineSight.Core.Contracts;
using LineSight.Core.Models;

namespace LineSight.Simulator;

/// <summary>
/// Store-and-forward sender: drains the outgoing queue in order and retries each message with
/// exponential backoff until the API accepts it, so an API outage delays data but doesn't lose it.
/// Order matters: a module's Laser Scribe step must never overtake its Deposition step.
/// </summary>
public sealed class ApiSender(HttpClient http, Action<string> log, TimeSpan initialBackoff, TimeSpan maxBackoff)
{
    public async Task RunAsync(ChannelReader<SimulatorMessage> reader, CancellationToken ct)
    {
        await foreach (var message in reader.ReadAllAsync(ct))
            await SendAsync(message, ct);
    }

    /// <summary>Returns true if delivered, false if the API permanently rejected it (4xx).</summary>
    public async Task<bool> SendAsync(SimulatorMessage message, CancellationToken ct)
    {
        using var response = await SendWithRetryAsync(token => message switch
        {
            MachineEventMessage m => http.PostAsJsonAsync("api/events", m.Request, LineSightJson.Options, token),
            ModuleStepMessage m => http.PostAsJsonAsync(
                $"api/modules/{Uri.EscapeDataString(m.SerialNumber)}/steps", m.Request, LineSightJson.Options, token),
            _ => throw new ArgumentOutOfRangeException(nameof(message), message, "Unknown message type.")
        }, ct);

        if (response.IsSuccessStatusCode)
            return true;

        var body = await response.Content.ReadAsStringAsync(ct);
        log($"Rejected by API ({(int)response.StatusCode}) - dropping {message}: {body}");
        return false;
    }

    /// <summary>Station list from the API (waits/retries until the API is up).</summary>
    public async Task<IReadOnlyList<Station>> GetStationsAsync(CancellationToken ct)
    {
        using var response = await SendWithRetryAsync(token => http.GetAsync("api/stations", token), ct);
        response.EnsureSuccessStatusCode();

        var stations = await response.Content.ReadFromJsonAsync<List<StationStatusResponse>>(LineSightJson.Options, ct)
                       ?? [];
        return stations.Select(s => new Station(s.Id, s.Name, s.Sequence, s.IdealCycleTimeSeconds)).ToList();
    }

    /// <summary>
    /// Retries network errors, timeouts, 5xx, 408 and 429 forever (with backoff).
    /// Other 4xx responses are returned immediately: the request itself is wrong and resending won't help.
    /// </summary>
    private async Task<HttpResponseMessage> SendWithRetryAsync(
        Func<CancellationToken, Task<HttpResponseMessage>> send, CancellationToken ct)
    {
        for (var attempt = 0; ; attempt++)
        {
            string failure;
            try
            {
                var response = await send(ct);
                if (!IsTransient(response.StatusCode))
                    return response;

                failure = $"HTTP {(int)response.StatusCode}";
                response.Dispose();
            }
            catch (HttpRequestException ex)
            {
                failure = ex.Message;
            }
            catch (TaskCanceledException) when (!ct.IsCancellationRequested)
            {
                failure = "request timed out";   // HttpClient.Timeout surfaces as TaskCanceledException
            }

            var delay = Backoff.Delay(attempt, initialBackoff, maxBackoff);
            log($"API unavailable ({failure}). Retry #{attempt + 1} in {delay.TotalSeconds:0.##}s");
            await Task.Delay(delay, ct);
        }
    }

    private static bool IsTransient(HttpStatusCode status) =>
        (int)status >= 500 || status is HttpStatusCode.RequestTimeout or HttpStatusCode.TooManyRequests;
}
