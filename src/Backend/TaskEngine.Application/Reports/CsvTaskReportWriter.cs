using System.Globalization;

namespace TaskEngine.Application.Reports;

/// <summary>
/// Formats <see cref="TaskReportRow"/> data as CSV text with RFC 4180 escaping (fields
/// containing a comma, quote, or line break are quoted, with internal quotes doubled). Pure text
/// formatting with no I/O of its own - the caller passes an already-open <see cref="TextWriter"/>
/// and decides the real destination (file, in-memory buffer, HTTP response, etc.), so this stays
/// in Application without needing an Infrastructure port. Column order keeps the original phase-1
/// columns (id..tempo_humano_segundos) first for compatibility, appending the RF-016 columns
/// (expedient, unmapped time/justifications, total invested) after them.
/// </summary>
public static class CsvTaskReportWriter
{
    private static readonly string[] Header =
    [
        "id", "inicio", "fim", "provedor", "tempo_ia_segundos", "tempo_humano_segundos",
        "tempo_expediente_segundos", "tempo_nao_mapeado_segundos", "justificativas_tempo_nao_mapeado",
        "tempo_total_investido_segundos",
    ];

    /// <summary>
    /// Separator used to join multiple unmapped-time justifications within a single CSV field.
    /// Not a delimiter that itself needs escaping - RFC 4180 quoting still applies to the field as
    /// a whole (e.g. if a justification contains a comma or quote).
    /// </summary>
    private const string JustificationSeparator = "; ";

    public static void Write(IEnumerable<TaskReportRow> rows, TextWriter writer)
    {
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentNullException.ThrowIfNull(writer);

        CsvWriting.WriteRow(writer, Header);

        foreach (TaskReportRow row in rows)
        {
            CsvWriting.WriteRow(
                writer,
                [
                    row.TaskId.ToString(),
                    FormatDate(row.StartedAt),
                    FormatDate(row.EndedAt),
                    row.ProviderId ?? string.Empty,
                    row.AiSeconds.ToString(CultureInfo.InvariantCulture),
                    row.HumanSeconds.ToString(CultureInfo.InvariantCulture),
                    row.ScheduledSeconds.ToString(CultureInfo.InvariantCulture),
                    row.UnmappedSeconds.ToString(CultureInfo.InvariantCulture),
                    string.Join(JustificationSeparator, row.UnmappedJustifications),
                    row.TotalSeconds.ToString(CultureInfo.InvariantCulture),
                ]);
        }
    }

    private static string FormatDate(DateOnly? date) =>
        date?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? string.Empty;
}
