using System.Globalization;

namespace TaskEngine.Application.Reports;

/// <summary>
/// Formats <see cref="TaskActivityTimelineRow"/> data as CSV text with RFC 4180 escaping (see
/// <see cref="CsvWriting"/>). Pure text formatting with no I/O of its own - same rationale as
/// <see cref="CsvTaskReportWriter"/>. Columns follow the reference prototype
/// (`docs/prototipo/TaskEngine Relatório.dc.html`) conceptually - path/address, time invested,
/// start - except <c>inicio</c> is a full round-trip timestamp rather than the prototype's
/// minutes-from-task-start (that was a chart-positioning artifact, not meaningful once exported on
/// its own).
/// </summary>
public static class CsvTaskActivityTimelineWriter
{
    private static readonly string[] Header = ["arquivo", "tempo_investido_segundos", "inicio"];

    public static void Write(IEnumerable<TaskActivityTimelineRow> rows, TextWriter writer)
    {
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentNullException.ThrowIfNull(writer);

        CsvWriting.WriteRow(writer, Header);

        foreach (TaskActivityTimelineRow row in rows)
        {
            CsvWriting.WriteRow(
                writer,
                [
                    row.Path ?? string.Empty,
                    row.Duration.TotalSeconds.ToString(CultureInfo.InvariantCulture),
                    row.StartedAt.ToString("o", CultureInfo.InvariantCulture),
                ]);
        }
    }
}
