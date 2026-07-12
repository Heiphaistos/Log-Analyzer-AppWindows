using WinLogAnalyzer.Core.Export;
using WinLogAnalyzer.Core.Reader;
using Xunit;

namespace WinLogAnalyzer.Tests;

public class ReportExporterTests
{
    private static string ExportOne(string message)
    {
        var entry = Make.Event(1, "Error", new DateTime(2026, 7, 13, 10, 0, 0)) with { Message = message };
        var path = Path.Combine(Path.GetTempPath(), $"wla_test_{Guid.NewGuid():N}.csv");
        try
        {
            ReportExporter.ToCsv(new[] { entry }, path);
            return File.ReadAllLines(path)[1]; // ligne de donnees
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Theory]
    [InlineData("=cmd|' /C calc'!A0")]
    [InlineData("+2+5")]
    [InlineData("@SUM(A1)")]
    [InlineData("-1+1")]
    public void ToCsv_neutralizes_formula_injection(string payload)
    {
        var line = ExportOne(payload);
        var message = line.Split(';')[^1].Trim('"');
        Assert.StartsWith("'", message);
    }

    [Fact]
    public void ToCsv_quotes_separator_and_flattens_newlines()
    {
        var line = ExportOne("a;b\nc");
        Assert.EndsWith("\"a;b c\"", line);
    }

    [Fact]
    public void ToCsv_leaves_plain_text_untouched()
    {
        var line = ExportOne("message simple");
        Assert.EndsWith(";message simple", line);
    }
}

public class BuildQueryTests
{
    [Fact]
    public void Default_levels_are_critical_and_error()
        => Assert.Equal("*[System[(Level=1 or Level=2)]]", EventLogService.BuildQuery(null));

    [Fact]
    public void Explicit_levels_are_joined()
        => Assert.Equal("*[System[(Level=1 or Level=2 or Level=3)]]",
            EventLogService.BuildQuery(new[] { 1, 2, 3 }));

    [Fact]
    public void Time_range_adds_timediff_clause_in_milliseconds()
        => Assert.Equal(
            "*[System[(Level=2) and TimeCreated[timediff(@SystemTime) <= 86400000]]]",
            EventLogService.BuildQuery(new[] { 2 }, maxAgeHours: 24));

    [Fact]
    public void Zero_or_null_range_omits_time_clause()
    {
        Assert.DoesNotContain("timediff", EventLogService.BuildQuery(new[] { 1 }, 0));
        Assert.DoesNotContain("timediff", EventLogService.BuildQuery(new[] { 1 }, null));
    }

    [Fact]
    public void Large_range_does_not_overflow()
        // 720 h (30 j) = 2 592 000 000 ms > int.MaxValue : doit rester exact.
        => Assert.Contains("2592000000", EventLogService.BuildQuery(new[] { 1 }, 720));
}
