using LineSight.Core.Abstractions;
using LineSight.Tests.Fakes;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace LineSight.Tests.Api;

/// <summary>
/// Runs the real API (routing, model binding, controllers, JSON) in memory, but swaps the
/// SQL Server repositories for in-memory fakes - so these tests need no database.
/// </summary>
public sealed class LineSightApiFactory : WebApplicationFactory<Program>
{
    public InMemoryModuleRepository Modules { get; } = new();
    public FakeMachineEventRepository Events { get; } = new();

    protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
    {
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IStationRepository>();
            services.RemoveAll<IModuleRepository>();
            services.RemoveAll<IMachineEventRepository>();
            services.RemoveAll<IOeeRepository>();

            services.AddSingleton<IStationRepository>(new FakeStationRepository());
            services.AddSingleton<IModuleRepository>(Modules);
            services.AddSingleton<IMachineEventRepository>(Events);
            services.AddSingleton<IOeeRepository>(new FakeOeeRepository());
        });
    }
}
