using System.Text.RegularExpressions;

namespace LineSight.Data;

/// <summary>Helpers for running .sql files written for sqlcmd/SSMS from code.</summary>
public static partial class SqlScript
{
    // "GO" alone on a line (any case, surrounding whitespace allowed).
    [GeneratedRegex(@"^\s*GO\s*$", RegexOptions.IgnoreCase | RegexOptions.Multiline)]
    private static partial Regex GoLine();

    /// <summary>
    /// Splits a script into batches on "GO" separator lines. GO is a sqlcmd/SSMS keyword,
    /// not T-SQL, so ADO.NET can't execute a script that contains it.
    /// </summary>
    public static IReadOnlyList<string> SplitBatches(string script) =>
        GoLine().Split(script.Replace("\r\n", "\n"))
            .Select(batch => batch.Trim())
            .Where(batch => batch.Length > 0)
            .ToList();

    [GeneratedRegex(@"^\s*(USE\s|IF\s+DB_ID\(.*\)\s+IS\s+NULL\s+CREATE\s+DATABASE\s)", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex DatabaseSelection();

    /// <summary>
    /// True for the "create/select the LineSight database" batches at the top of schema.sql.
    /// They exist for sqlcmd users; when the app runs a script it is already connected to its target
    /// database (whatever it is named), so it skips them.
    /// </summary>
    public static bool SelectsDatabase(string batch) => DatabaseSelection().IsMatch(WithoutLeadingComments(batch));

    // Skips whitespace, "-- line" and "/* block */" comments at the start of a batch.
    [GeneratedRegex(@"^(\s+|--[^\n]*(\n|$)|/\*.*?\*/)*", RegexOptions.Singleline)]
    private static partial Regex LeadingComments();

    private static string WithoutLeadingComments(string batch) => LeadingComments().Replace(batch, "", 1);

    /// <summary>Reads one of the database/*.sql files embedded in this assembly.</summary>
    public static string ReadEmbedded(string fileName)
    {
        var name = $"LineSight.Data.Scripts.{fileName}";
        using var stream = typeof(SqlScript).Assembly.GetManifestResourceStream(name)
            ?? throw new InvalidOperationException($"Embedded script '{name}' not found.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
