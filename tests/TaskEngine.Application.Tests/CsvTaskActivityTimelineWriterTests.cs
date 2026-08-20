using System.Globalization;
using TaskEngine.Application.Reports;
using TaskEngine.Domain.Entities;

namespace TaskEngine.Application.Tests;

public class CsvTaskActivityTimelineWriterTests
{
    private const string Header = "arquivo,tempo_investido_segundos,inicio\r\n";

    [Fact]
    public void Write_NoRows_WritesOnlyHeader()
    {
        using var writer = new StringWriter();

        CsvTaskActivityTimelineWriter.Write([], writer);

        Assert.Equal(Header, writer.ToString());
    }

    [Fact]
    public void Write_NormalRow_FormatsFieldsCorrectly()
    {
        using var writer = new StringWriter();
        var startedAt = new DateTimeOffset(2026, 1, 10, 9, 0, 0, TimeSpan.Zero);
        var row = new TaskActivityTimelineRow(
            Guid.NewGuid(), ActivityItemType.File, "src/a.cs", ActivitySource.Human,
            startedAt, startedAt.AddMinutes(30));

        CsvTaskActivityTimelineWriter.Write([row], writer);

        var expected = Header + $"src/a.cs,1800,{startedAt.ToString("o", CultureInfo.InvariantCulture)}\r\n";
        Assert.Equal(expected, writer.ToString());
    }

    [Fact]
    public void Write_NullPath_WritesEmptyField()
    {
        using var writer = new StringWriter();
        var startedAt = new DateTimeOffset(2026, 1, 10, 9, 0, 0, TimeSpan.Zero);
        var row = new TaskActivityTimelineRow(
            Guid.NewGuid(), null, null, ActivitySource.Ai, startedAt, startedAt.AddMinutes(1));

        CsvTaskActivityTimelineWriter.Write([row], writer);

        var expected = Header + $",60,{startedAt.ToString("o", CultureInfo.InvariantCulture)}\r\n";
        Assert.Equal(expected, writer.ToString());
    }

    [Fact]
    public void Write_PathWithCommaAndQuotes_IsEscapedPerRfc4180()
    {
        using var writer = new StringWriter();
        var startedAt = new DateTimeOffset(2026, 1, 10, 9, 0, 0, TimeSpan.Zero);
        var row = new TaskActivityTimelineRow(
            Guid.NewGuid(), ActivityItemType.Browser, "https://a.com?x=\"1,2\"", ActivitySource.Human,
            startedAt, startedAt.AddMinutes(1));

        CsvTaskActivityTimelineWriter.Write([row], writer);

        Assert.Contains("\"https://a.com?x=\"\"1,2\"\"\"", writer.ToString());
    }

    [Fact]
    public void Write_MultipleRows_PreservesGivenOrder()
    {
        using var writer = new StringWriter();
        var startedAt = new DateTimeOffset(2026, 1, 10, 9, 0, 0, TimeSpan.Zero);
        var first = new TaskActivityTimelineRow(
            Guid.NewGuid(), ActivityItemType.File, "src/a.cs", ActivitySource.Human,
            startedAt, startedAt.AddMinutes(10));
        var second = new TaskActivityTimelineRow(
            Guid.NewGuid(), ActivityItemType.File, "src/b.cs", ActivitySource.Human,
            startedAt.AddMinutes(5), startedAt.AddMinutes(20));

        CsvTaskActivityTimelineWriter.Write([first, second], writer);

        var lines = writer.ToString().Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(3, lines.Length);
        Assert.StartsWith("src/a.cs,", lines[1]);
        Assert.StartsWith("src/b.cs,", lines[2]);
    }

    [Fact]
    public void Write_Seconds_UseDotAsDecimalSeparatorRegardlessOfCurrentCulture()
    {
        var originalCulture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("pt-BR");

            using var writer = new StringWriter();
            var startedAt = new DateTimeOffset(2026, 1, 10, 9, 0, 0, TimeSpan.Zero);
            var row = new TaskActivityTimelineRow(
                Guid.NewGuid(), ActivityItemType.File, "src/a.cs", ActivitySource.Human,
                startedAt, startedAt.AddSeconds(90.5));

            CsvTaskActivityTimelineWriter.Write([row], writer);

            var content = writer.ToString();
            Assert.Contains("90.5", content);
            Assert.DoesNotContain("90,5", content);
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
        }
    }
}
