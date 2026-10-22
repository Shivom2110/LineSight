using LineSight.Core.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace LineSight.Data;

public static class ServiceCollectionExtensions
{
    /// <summary>Registers the SQL Server implementations of the Core repository interfaces.</summary>
    public static IServiceCollection AddLineSightData(this IServiceCollection services, string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        services.AddSingleton(new SqlConnectionFactory(connectionString));
        services.AddScoped<IStationRepository, SqlStationRepository>();
        services.AddScoped<IMachineEventRepository, SqlMachineEventRepository>();
        services.AddScoped<IModuleRepository, SqlModuleRepository>();
        services.AddScoped<IOeeRepository, SqlOeeRepository>();
        return services;
    }
}
