using LineSight.Data;

namespace LineSight.Tests.Data;

public class SqlScriptTests
{
    [Fact]
    public void SplitBatches_SplitsOnGoLines_AndDropsEmptyBatches()
    {
        const string script = "CREATE TABLE A (Id INT);\nGO\n\nCREATE TABLE B (Id INT);\n  go  \nGO\n";

        var batches = SqlScript.SplitBatches(script);

        Assert.Equal(["CREATE TABLE A (Id INT);", "CREATE TABLE B (Id INT);"], batches);
    }

    [Fact]
    public void SplitBatches_IgnoresGoInsideOtherText()
    {
        // "GO" only counts when it's alone on its line.
        const string script = "SELECT 'GO' AS Word; -- GO team\nSELECT GOOD = 1;\nGO";

        var batch = Assert.Single(SqlScript.SplitBatches(script));

        Assert.Contains("SELECT GOOD = 1;", batch);
    }

    [Fact]
    public void SplitBatches_HandlesWindowsLineEndings()
    {
        var batches = SqlScript.SplitBatches("SELECT 1;\r\nGO\r\nSELECT 2;\r\nGO\r\n");

        Assert.Equal(2, batches.Count);
        Assert.DoesNotContain(batches, b => b.Contains('\r'));
    }

    [Fact]
    public void EmbeddedScripts_ContainTheStoredProcedures()
    {
        // The real database scripts ship inside LineSight.Data so the app can set up its own database.
        var procedures = SqlScript.SplitBatches(SqlScript.ReadEmbedded("procedures.sql"));

        Assert.Contains(procedures, b => b.Contains("CREATE OR ALTER PROCEDURE dbo.usp_GetStationOEE"));
        Assert.Contains(procedures, b => b.Contains("CREATE OR ALTER PROCEDURE dbo.usp_GetModuleGenealogy"));
        Assert.Contains(procedures, b => b.Contains("CREATE OR ALTER PROCEDURE dbo.usp_RecordMachineEvent"));
    }
}
