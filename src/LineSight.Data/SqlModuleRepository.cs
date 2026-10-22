using System.Data;
using Dapper;
using LineSight.Core.Abstractions;
using LineSight.Core.Models;

namespace LineSight.Data;

public sealed class SqlModuleRepository(SqlConnectionFactory connections) : IModuleRepository
{
    public async Task<Module?> GetBySerialAsync(string serialNumber, CancellationToken ct = default)
    {
        var parameters = new DynamicParameters();
        parameters.Add("@SerialNumber", serialNumber, DbType.AnsiString, size: 20);

        await using var connection = connections.Create();
        // usp_GetModuleGenealogy returns two result sets: the module header, then its steps.
        await using var results = await connection.QueryMultipleAsync(new CommandDefinition(
            "dbo.usp_GetModuleGenealogy", parameters, commandType: CommandType.StoredProcedure, cancellationToken: ct));

        var header = await results.ReadSingleOrDefaultAsync<ModuleRow>();
        var steps = (await results.ReadAsync<StepRow>()).ToList();

        if (header is null)
            return null;

        return new Module(
            header.SerialNumber,
            SqlTypes.ParseEnum<ModuleStatus>(header.Status),
            SqlTypes.AsUtc(header.CreatedUtc),
            steps.Select(s => new ModuleStep(
                    s.StationId, s.StationName, s.StationSequence,
                    SqlTypes.ParseEnum<StepResult>(s.Result), SqlTypes.AsUtc(s.TimestampUtc)))
                .ToList());
    }

    public async Task<IReadOnlyList<ModuleSummary>> GetRecentAsync(int take, CancellationToken ct = default)
    {
        await using var connection = connections.Create();
        var rows = await connection.QueryAsync<SummaryRow>(new CommandDefinition(
            "dbo.usp_GetRecentModules", new { Take = take }, commandType: CommandType.StoredProcedure, cancellationToken: ct));

        return rows.Select(r => new ModuleSummary(
                r.SerialNumber,
                SqlTypes.ParseEnum<ModuleStatus>(r.Status),
                SqlTypes.AsUtc(r.CreatedUtc),
                r.LastStation,
                SqlTypes.AsUtc(r.LastStepUtc)))
            .ToList();
    }

    public async Task RecordStepAsync(string serialNumber, int stationId, StepResult result, DateTime timestampUtc,
        ModuleStatus newStatus, CancellationToken ct = default)
    {
        var parameters = new DynamicParameters();
        parameters.Add("@SerialNumber", serialNumber, DbType.AnsiString, size: 20);
        parameters.Add("@StationId", stationId);
        parameters.Add("@Result", result.ToString(), DbType.AnsiString, size: 5);
        parameters.Add("@TimestampUtc", timestampUtc, DbType.DateTime2);
        parameters.Add("@NewStatus", newStatus.ToString(), DbType.AnsiString, size: 12);

        await using var connection = connections.Create();
        await connection.ExecuteAsync(new CommandDefinition(
            "dbo.usp_RecordModuleStep", parameters, commandType: CommandType.StoredProcedure, cancellationToken: ct));
    }

    private sealed class ModuleRow
    {
        public string SerialNumber { get; set; } = "";
        public string Status { get; set; } = "";
        public DateTime CreatedUtc { get; set; }
    }

    private sealed class SummaryRow
    {
        public string SerialNumber { get; set; } = "";
        public string Status { get; set; } = "";
        public DateTime CreatedUtc { get; set; }
        public string? LastStation { get; set; }
        public DateTime? LastStepUtc { get; set; }
    }

    private sealed class StepRow
    {
        public int StationId { get; set; }
        public string StationName { get; set; } = "";
        public int StationSequence { get; set; }
        public string Result { get; set; } = "";
        public DateTime TimestampUtc { get; set; }
    }
}
