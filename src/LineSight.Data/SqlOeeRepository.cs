using System.Data;
using Dapper;
using LineSight.Core.Abstractions;
using LineSight.Core.Oee;

namespace LineSight.Data;

/// <summary>
/// Calls usp_GetStationOEE, which does the heavy, set-based aggregation next to the data.
/// We only take the raw aggregates; the OEE math itself is done by the unit-tested OeeCalculator.
/// </summary>
public sealed class SqlOeeRepository(SqlConnectionFactory connections) : IOeeRepository
{
    public async Task<StationOeeData?> GetStationOeeDataAsync(
        int stationId, DateTime fromUtc, DateTime toUtc, CancellationToken ct = default)
    {
        var parameters = new DynamicParameters();
        parameters.Add("@StationId", stationId);
        parameters.Add("@FromUtc", fromUtc, DbType.DateTime2);
        parameters.Add("@ToUtc", toUtc, DbType.DateTime2);

        await using var connection = connections.Create();
        await using var results = await connection.QueryMultipleAsync(new CommandDefinition(
            "dbo.usp_GetStationOEE", parameters, commandType: CommandType.StoredProcedure, cancellationToken: ct));

        var aggregate = await results.ReadSingleOrDefaultAsync<AggregateRow>();
        var downtime = (await results.ReadAsync<DowntimeRow>()).ToList();

        if (aggregate is null)
            return null;   // no row = station doesn't exist

        var input = new OeeInput(
            aggregate.PlannedProductionTimeSeconds,
            aggregate.RunTimeSeconds,
            aggregate.IdealCycleTimeSeconds,
            aggregate.GoodCount,
            aggregate.ScrapCount);

        return new StationOeeData(
            aggregate.StationName,
            input,
            downtime.Select(d => new DowntimeSummary(d.Reason, (double)d.DurationSeconds, d.Occurrences)).ToList());
    }

    private sealed class AggregateRow
    {
        public string StationName { get; set; } = "";
        public double IdealCycleTimeSeconds { get; set; }
        public double PlannedProductionTimeSeconds { get; set; }
        public double RunTimeSeconds { get; set; }
        public int GoodCount { get; set; }
        public int ScrapCount { get; set; }
    }

    private sealed class DowntimeRow
    {
        public string Reason { get; set; } = "";
        public decimal DurationSeconds { get; set; }
        public int Occurrences { get; set; }
    }
}
