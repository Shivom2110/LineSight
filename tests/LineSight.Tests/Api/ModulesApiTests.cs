using System.Net;
using System.Net.Http.Json;
using LineSight.Core.Contracts;
using LineSight.Core.Models;
using LineSight.Core.Traceability;
using LineSight.Tests.Fakes;

namespace LineSight.Tests.Api;

public sealed class ModulesApiTests : IDisposable
{
    private const string Serial = "FS-20261022-00001";

    private readonly LineSightApiFactory _factory = new();
    private readonly HttpClient _client;

    public ModulesApiTests() => _client = _factory.CreateClient();

    public void Dispose() => _factory.Dispose();

    private Task<HttpResponseMessage> PostStep(Station station, StepResult result = StepResult.Pass, string serial = Serial) =>
        _client.PostAsJsonAsync($"/api/modules/{serial}/steps",
            new RecordModuleStepRequest(station.Id, result), LineSightJson.Options);

    [Fact]
    public async Task StepsInOrder_Return200_WithNewStatus()
    {
        var response = await PostStep(TestLine.Deposition);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<RecordedStepResponse>(LineSightJson.Options);
        Assert.Equal(ModuleStatus.InProgress, body!.Status);
    }

    [Fact]
    public async Task SkippedStation_Returns409()
    {
        await PostStep(TestLine.Deposition);

        var response = await PostStep(TestLine.Lamination);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task StepOnScrappedModule_Returns409()
    {
        await PostStep(TestLine.Deposition);
        await PostStep(TestLine.LaserScribe, StepResult.Scrap);

        var response = await PostStep(TestLine.Lamination);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task InvalidSerial_Returns400()
    {
        var response = await PostStep(TestLine.Deposition, serial: "NOT-A-SERIAL");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Genealogy_ForUnknownSerial_Returns404()
    {
        var response = await _client.GetAsync("/api/modules/FS-20261022-99999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Genealogy_ReturnsEveryStepInOrder()
    {
        foreach (var station in TestLine.Stations)
            await PostStep(station);

        var genealogy = await _client.GetFromJsonAsync<ModuleGenealogyResponse>($"/api/modules/{Serial}", LineSightJson.Options);

        Assert.NotNull(genealogy);
        Assert.Equal(ModuleStatus.Completed, genealogy.Status);
        Assert.Equal([1, 2, 3, 4, 5], genealogy.Steps.Select(s => s.StationSequence));
    }
}
