using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using LineSight.Core.Contracts;

namespace LineSight.Dashboard.Services;

public sealed class LineSightApiClient(HttpClient http) : ILineSightApiClient
{
    public async Task<IReadOnlyList<StationStatusResponse>> GetStationsAsync(CancellationToken ct) =>
        await http.GetFromJsonAsync<List<StationStatusResponse>>("api/stations", LineSightJson.Options, ct) ?? [];

    public Task<OeeResponse?> GetOeeAsync(int stationId, DateTime fromUtc, DateTime toUtc, CancellationToken ct)
    {
        var from = Uri.EscapeDataString(fromUtc.ToString("O"));
        var to = Uri.EscapeDataString(toUtc.ToString("O"));
        return http.GetFromJsonAsync<OeeResponse>($"api/oee/{stationId}?from={from}&to={to}", LineSightJson.Options, ct);
    }

    public async Task<ModuleGenealogyResponse?> GetModuleAsync(string serialNumber, CancellationToken ct)
    {
        using var response = await http.GetAsync($"api/modules/{Uri.EscapeDataString(serialNumber)}", ct);
        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ModuleGenealogyResponse>(LineSightJson.Options, ct);
    }
}
