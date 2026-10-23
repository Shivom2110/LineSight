using LineSight.Core.Abstractions;
using LineSight.Tests.Fakes;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Time.Testing;

namespace LineSight.Tests.Api;

/// <summary>
/// Runs the real API (routing, model binding, controllers, JSON) in memory, but swaps the
/// SQL Server repositories for in-memory fakes - so these tests need no database.
/// </summary>
public sealed class LineSightApiFactory : WebApplicationFactory<Program>
{
    public InMemoryModuleRepository Modules { get; } = new();
    public FakeMachineEventRepository Events { get; } = new();

    /// <summary>Optional controllable clock (replaces TimeProvider.System when set).</summary>
    public FakeTimeProvider? Clock { get; init; }

    /// <summary>Optional configuration overrides, e.g. a fast simulation tick for tests.</summary>
    public Dictionary<string, string?> Settings { get; init; } = [];

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        foreach (var (key, value) in Settings)
            builder.UseSetting(key, value);

        builder.ConfigureTestServices(services =>
        {
            if (Clock is not null)
            {
                services.RemoveAll<TimeProvider>();
                services.AddSingleton<TimeProvider>(Clock);
            }

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
