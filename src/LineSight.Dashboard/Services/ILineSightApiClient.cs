using LineSight.Core.Contracts;

namespace LineSight.Dashboard.Services;

/// <summary>What the dashboard needs from the MES API (an interface so ViewModels can be tested with a fake).</summary>
public interface ILineSightApiClient
{
    Task<IReadOnlyList<StationStatusResponse>> GetStationsAsync(CancellationToken ct);

    Task<OeeResponse?> GetOeeAsync(int stationId, DateTime fromUtc, DateTime toUtc, CancellationToken ct);

    /// <summary>The module's genealogy, or null if the serial is unknown.</summary>
    Task<ModuleGenealogyResponse?> GetModuleAsync(string serialNumber, CancellationToken ct);
}
