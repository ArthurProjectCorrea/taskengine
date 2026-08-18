using System.Globalization;

namespace TaskEngine.Application.Reports;

/// <summary>
/// Formats <see cref="TaskReportRow"/> data as CSV text with RFC 4180 escaping (fields
/// containing a comma, quote, or line break are quoted, with internal quotes doubled). Pure text
/// formatting with no I/O of its own - the caller passes an already-open <see cref="TextWriter"/>
/// and decides the real destination (file, in-memory buffer, HTTP response, etc.), so this stays
/// in Application without needing an Infrastructure port. The escaping logic is intentionally
/// generic (not just "does this need it for these columns") so phase 2 (#41), which adds a
/// free-text justification column, can reuse it as-is.
/// </summary>
public static class CsvTaskReportWriter
{
    private static readonly string[] Header =
        ["id", "inicio", "fim", "provedor", "tempo_ia_segundos", "tempo_humano_segundos"];

    public static void Write(IEnumerable<TaskReportRow> rows, TextWriter writer)
    {
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentNullException.ThrowIfNull(writer);

        WriteRow(writer, Header);

        foreach (TaskReportRow row in rows)
        {
            WriteRow(
                writer,
                [
                    row.TaskId.ToString(),
                    FormatDate(row.StartedAt),
                    FormatDate(row.EndedAt),
                    row.ProviderId ?? string.Empty,
                    row.AiSeconds.ToString(CultureInfo.InvariantCulture),
                    row.HumanSeconds.ToString(CultureInfo.InvariantCulture),
                ]);
        }
    }

    private static void WriteRow(TextWriter writer, IReadOnlyList<string> fields)
    {
        for (var i = 0; i < fields.Count; i++)
        {
            if (i > 0)
            {
                writer.Write(',');
            }

            writer.Write(Escape(fields[i]));
        }

        writer.Write("\r\n");
    }

    private static string FormatDate(DateOnly? date) =>
        date?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? string.Empty;

    private static string Escape(string field)
    {
        if (field.IndexOfAny([',', '"', '\r', '\n']) < 0)
        {
            return field;
        }

        return $"\"{field.Replace("\"", "\"\"")}\"";
    }
}
