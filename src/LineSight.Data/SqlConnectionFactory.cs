using Microsoft.Data.SqlClient;

namespace LineSight.Data;

/// <summary>
/// Creates connections from the configured connection string. ADO.NET pools them under the hood.
///
/// Every connection and command gets SqlClient's built-in exponential retry for transient errors.
/// That matters in the cloud: a serverless Azure SQL database pauses when idle, and the first
/// request after a pause gets error 40613 ("database not currently available") while it resumes.
/// Retrying turns that into a slow first request instead of a failed one.
/// </summary>
public sealed class SqlConnectionFactory(string connectionString)
{
    private static readonly SqlRetryLogicBaseProvider Retry =
        SqlConfigurableRetryFactory.CreateExponentialRetryProvider(new SqlRetryLogicOption
        {
            NumberOfTries = 6,                                // ~1 minute in total, enough for a resume
            DeltaTime = TimeSpan.FromSeconds(2),
            MaxTimeInterval = TimeSpan.FromSeconds(20)
            // TransientErrors left null = SqlClient's default list (40613, 40197, 40501, 49918, ...)
        });

    public SqlConnection Create() => new(connectionString) { RetryLogicProvider = Retry };
}
