using System.Globalization;
using TaskEngine.Application.Reports;

namespace TaskEngine.Application.Tests;

public class CsvTaskReportWriterTests
{
    [Fact]
    public void Write_NoRows_WritesOnlyHeader()
    {
        using var writer = new StringWriter();

        CsvTaskReportWriter.Write([], writer);

        Assert.Equal("id,inicio,fim,provedor,tempo_ia_segundos,tempo_humano_segundos\r\n", writer.ToString());
    }

    [Fact]
    public void Write_NormalRow_FormatsFieldsCorrectly()
    {
        using var writer = new StringWriter();
        var taskId = Guid.NewGuid();
        var row = new TaskReportRow(
            taskId,
            new DateOnly(2026, 1, 10),
            new DateOnly(2026, 1, 15),
            "github",
            125.5,
            3600);

        CsvTaskReportWriter.Write([row], writer);

        var expected = "id,inicio,fim,provedor,tempo_ia_segundos,tempo_humano_segundos\r\n"
            + $"{taskId},2026-01-10,2026-01-15,github,125.5,3600\r\n";
        Assert.Equal(expected, writer.ToString());
    }

    [Fact]
    public void Write_RowWithNullDatesAndProviderId_WritesEmptyFields()
    {
        using var writer = new StringWriter();
        var taskId = Guid.NewGuid();
        var row = new TaskReportRow(taskId, null, null, null, 0, 0);

        CsvTaskReportWriter.Write([row], writer);

        var expected = "id,inicio,fim,provedor,tempo_ia_segundos,tempo_humano_segundos\r\n"
            + $"{taskId},,,,0,0\r\n";
        Assert.Equal(expected, writer.ToString());
    }

    [Fact]
    public void Write_ProviderIdWithCommaAndQuotes_IsEscapedPerRfc4180()
    {
        using var writer = new StringWriter();
        var taskId = Guid.NewGuid();
        var row = new TaskReportRow(taskId, null, null, "acme, \"gh\" provider", 0, 0);

        CsvTaskReportWriter.Write([row], writer);

        var expected = "id,inicio,fim,provedor,tempo_ia_segundos,tempo_humano_segundos\r\n"
            + $"{taskId},,,\"acme, \"\"gh\"\" provider\",0,0\r\n";
        Assert.Equal(expected, writer.ToString());
    }

    [Fact]
    public void Write_ProviderIdWithLineBreak_IsQuoted()
    {
        using var writer = new StringWriter();
        var taskId = Guid.NewGuid();
        var row = new TaskReportRow(taskId, null, null, "line1\nline2", 0, 0);

        CsvTaskReportWriter.Write([row], writer);

        Assert.Contains("\"line1\nline2\"", writer.ToString());
    }

    [Fact]
    public void Write_Seconds_UseDotAsDecimalSeparatorRegardlessOfCurrentCulture()
    {
        var originalCulture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("pt-BR");

            using var writer = new StringWriter();
            var row = new TaskReportRow(Guid.NewGuid(), null, null, null, 12.5, 30.25);

            CsvTaskReportWriter.Write([row], writer);

            var content = writer.ToString();
            Assert.Contains("12.5", content);
            Assert.Contains("30.25", content);
            Assert.DoesNotContain("12,5", content);
            Assert.DoesNotContain("30,25", content);
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
        }
    }
}
