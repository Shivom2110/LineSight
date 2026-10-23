using Dapper;
using Microsoft.Extensions.Logging;

namespace LineSight.Data;

/// <summary>
/// Lets the app set up its own database on startup (used for the cloud deployment):
///  - empty database  -> run schema.sql once (tables, indexes, seed stations)
///  - every startup   -> run procedures.sql (CREATE OR ALTER, so stored procedures always match the deployed code)
/// Existing tables are never dropped, so data survives redeploys.
/// </summary>
public sealed class DatabaseInitializer(SqlConnectionFactory connections, ILogger<DatabaseInitializer> logger)
{
    public async Task InitializeAsync(CancellationToken ct = default)
    {
        await using var connection = connections.Create();
        await connection.OpenAsync(ct);

        var hasSchema = await connection.ExecuteScalarAsync<int>(
            "SELECT CASE WHEN OBJECT_ID(N'dbo.Stations', N'U') IS NULL THEN 0 ELSE 1 END") == 1;

        if (!hasSchema)
        {
            logger.LogInformation("Empty database: creating schema and seed data");
            await RunScriptAsync(connection, "schema.sql", ct);
        }

        await RunScriptAsync(connection, "procedures.sql", ct);
        logger.LogInformation("Database ready (stored procedures up to date)");
    }

    private static async Task RunScriptAsync(System.Data.Common.DbConnection connection, string fileName, CancellationToken ct)
    {
        var batches = SqlScript.SplitBatches(SqlScript.ReadEmbedded(fileName)).Where(b => !SqlScript.SelectsDatabase(b));
        foreach (var batch in batches)
            await connection.ExecuteAsync(new CommandDefinition(batch, commandTimeout: 120, cancellationToken: ct));
    }
}
