using Microsoft.Data.SqlClient;

namespace LineSight.Data;

/// <summary>Creates connections from the configured connection string. ADO.NET pools them under the hood.</summary>
public sealed class SqlConnectionFactory(string connectionString)
{
    public SqlConnection Create() => new(connectionString);
}
