namespace LineSight.Data;

/// <summary>Helpers for running .sql files written for sqlcmd/SSMS from code.</summary>
public static class SqlScript
{
    /// <summary>
    /// Splits a script into batches on "GO" separator lines. GO is a sqlcmd/SSMS keyword,
    /// not T-SQL, so ADO.NET can't execute a script that contains it.
    /// </summary>
    public static IReadOnlyList<string> SplitBatches(string script) => throw new NotImplementedException();

    /// <summary>Reads one of the database/*.sql files embedded in this assembly.</summary>
    public static string ReadEmbedded(string fileName) => throw new NotImplementedException();
}
