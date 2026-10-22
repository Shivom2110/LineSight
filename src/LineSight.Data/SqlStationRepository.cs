using System.Data;
using Dapper;
using LineSight.Core.Abstractions;
using LineSight.Core.Models;

namespace LineSight.Data;

public sealed class SqlStationRepository(SqlConnectionFactory connections) : IStationRepository
{
    public async Task<IReadOnlyList<Station>> GetAllAsync(CancellationToken ct = default) =>
        (await GetStatusesAsync(ct)).Select(s => s.Station).ToList();

    public async Task<IReadOnlyList<StationStatus>> GetStatusesAsync(CancellationToken ct = default)
    {
        await using var connection = connections.Create();
        var rows = await connection.QueryAsync<StationRow>(new CommandDefinition(
            "dbo.usp_GetStations", commandType: CommandType.StoredProcedure, cancellationToken: ct));

        return rows.Select(r => new StationStatus(
                new Station(r.Id, r.Name, r.Sequence, r.IdealCycleTimeSeconds),
                r.CurrentState is null ? null : SqlTypes.ParseEnum<StationState>(r.CurrentState),
                r.DowntimeReason,
                SqlTypes.AsUtc(r.LastEventUtc)))
            .ToList();
    }

    /// <summary>Row shape returned by usp_GetStations (Dapper maps columns to properties by name).</summary>
    private sealed class StationRow
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public int Sequence { get; set; }
        public double IdealCycleTimeSeconds { get; set; }
        public string? CurrentState { get; set; }
        public string? DowntimeReason { get; set; }
        public DateTime? LastEventUtc { get; set; }
    }
}
