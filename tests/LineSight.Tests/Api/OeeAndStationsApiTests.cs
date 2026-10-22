using System.Net;
using System.Net.Http.Json;
using LineSight.Core.Contracts;
using LineSight.Tests.Fakes;

namespace LineSight.Tests.Api;

public sealed class OeeAndStationsApiTests : IDisposable
{
    private readonly LineSightApiFactory _factory = new();
    private readonly HttpClient _client;

    public OeeAndStationsApiTests() => _client = _factory.CreateClient();

    public void Dispose() => _factory.Dispose();

    [Fact]
    public async Task Oee_ForUnknownStation_Returns404()
    {
        var response = await _client.GetAsync("/api/oee/999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Oee_ForKnownStation_ReturnsBreakdownComputedByCore()
    {
        var oee = await _client.GetFromJsonAsync<OeeResponse>(
            $"/api/oee/{TestLine.Lamination.Id}?from=2026-10-22T08:00:00Z&to=2026-10-22T09:00:00Z", LineSightJson.Options);

        Assert.NotNull(oee);
        Assert.Equal("Lamination", oee.StationName);
        Assert.Equal(0.9, oee.Availability, precision: 4);
        Assert.Equal(0.8167, oee.Oee, precision: 4);
        Assert.Equal(102, oee.TotalCount);
        Assert.Null(oee.Warning);
        Assert.Equal("Equipment Fault", Assert.Single(oee.Downtime).Reason);
    }

    [Fact]
    public async Task Oee_WithFromAfterTo_Returns400()
    {
        var response = await _client.GetAsync(
            $"/api/oee/{TestLine.Lamination.Id}?from=2026-10-22T09:00:00Z&to=2026-10-22T08:00:00Z");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Stations_ReturnsAllFiveInLineOrder()
    {
        var stations = await _client.GetFromJsonAsync<List<StationStatusResponse>>("/api/stations", LineSightJson.Options);

        Assert.NotNull(stations);
        Assert.Equal(TestLine.Stations.Select(s => s.Name), stations.Select(s => s.Name));
    }

    [Fact]
    public async Task Root_ServesTheBrowserDashboard()
    {
        var response = await _client.GetAsync("/");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/html", response.Content.Headers.ContentType?.MediaType);
        Assert.Contains("LineSight", await response.Content.ReadAsStringAsync());
    }
}
