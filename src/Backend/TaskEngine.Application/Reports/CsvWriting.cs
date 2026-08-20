namespace TaskEngine.Application.Reports;

/// <summary>
/// Small shared RFC 4180 CSV-writing primitives used by both <see cref="CsvTaskReportWriter"/>
/// and <see cref="CsvTaskActivityTimelineWriter"/>, so the escaping/row-writing logic (fields
/// containing a comma, quote, or line break get quoted, with internal quotes doubled) lives in one
/// place.
/// </summary>
internal static class CsvWriting
{
    public static void WriteRow(TextWriter writer, IReadOnlyList<string> fields)
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

    private static string Escape(string field)
    {
        if (field.IndexOfAny([',', '"', '\r', '\n']) < 0)
        {
            return field;
        }

        return $"\"{field.Replace("\"", "\"\"")}\"";
    }
}
