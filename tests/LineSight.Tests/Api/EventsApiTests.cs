using System.Net;
using System.Net.Http.Json;
using System.Text;
using LineSight.Core.Contracts;
using LineSight.Core.Models;
using LineSight.Tests.Fakes;

namespace LineSight.Tests.Api;

public sealed class EventsApiTests : IDisposable
{
    private readonly LineSightApiFactory _factory = new();
    private readonly HttpClient _client;

    public EventsApiTests() => _client = _factory.CreateClient();

    public void Dispose() => _factory.Dispose();

    private Task<HttpResponseMessage> PostJson(string json) =>
        _client.PostAsync("/api/events", new StringContent(json, Encoding.UTF8, "application/json"));

    [Fact]
    public async Task ValidEvent_Returns201_AndIsStored()
    {
        var request = new RecordMachineEventRequest(TestLine.Deposition.Id, StationState.Running, null, 3, 1);

        var response = await _client.PostAsJsonAsync("/api/events", request, LineSightJson.Options);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var stored = Assert.Single(_factory.Events.Events);
        Assert.Equal(3, stored.GoodCount);
        Assert.Equal(DateTimeKind.Utc, stored.TimestampUtc.Kind);
    }

    [Fact]
    public async Task DownWithoutReason_Returns400()
    {
        var response = await PostJson("""{"stationId":10,"state":"Down","goodCount":0,"scrapCount":0}""");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("DowntimeReason", await response.Content.ReadAsStringAsync());
        Assert.Empty(_factory.Events.Events);
    }

    [Theory]
    [InlineData("""{"stationId":10,"state":"Exploded","goodCount":1,"scrapCount":0}""")]   // unknown state
    [InlineData("""{"stationId":10,"state":"Running","goodCount":-3,"scrapCount":0}""")]   // negative count
    [InlineData("""{"stationId":10,"state":"Running","goodCount":"many"}""")]              // wrong type
    [InlineData("this is not json")]
    public async Task BadPayload_Returns400(string json)
    {
        var response = await PostJson(json);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(_factory.Events.Events);
    }

    [Fact]
    public async Task UnknownStation_Returns400()
    {
        var response = await PostJson("""{"stationId":999,"state":"Running","goodCount":1,"scrapCount":0}""");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("StationId", await response.Content.ReadAsStringAsync());
    }
}
