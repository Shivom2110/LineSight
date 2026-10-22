using System.Data;
using Dapper;
using LineSight.Core.Abstractions;
using LineSight.Core.Models;

namespace LineSight.Data;

public sealed class SqlMachineEventRepository(SqlConnectionFactory connections) : IMachineEventRepository
{
    public async Task<long> RecordAsync(MachineEvent machineEvent, CancellationToken ct = default)
    {
        var parameters = new DynamicParameters();
        parameters.Add("@StationId", machineEvent.StationId);
        parameters.Add("@State", machineEvent.State.ToString(), DbType.AnsiString, size: 10);
        parameters.Add("@DowntimeReason", machineEvent.DowntimeReason, DbType.String, size: DowntimeReasons.MaxLength);
        parameters.Add("@GoodCount", machineEvent.GoodCount);
        parameters.Add("@ScrapCount", machineEvent.ScrapCount);
        // Explicit DateTime2: Dapper would otherwise send legacy DATETIME (~3 ms precision).
        parameters.Add("@TimestampUtc", machineEvent.TimestampUtc, DbType.DateTime2);

        await using var connection = connections.Create();
        return await connection.ExecuteScalarAsync<long>(new CommandDefinition(
            "dbo.usp_RecordMachineEvent", parameters, commandType: CommandType.StoredProcedure, cancellationToken: ct));
    }
}
