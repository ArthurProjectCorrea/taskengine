using System.Globalization;

namespace TaskEngine.Infrastructure.Persistence;

/// <summary>
/// SQLite has no native date/time type - <see cref="DateTimeOffset"/> values are stored as
/// round-trip ("O") ISO 8601 TEXT columns. Centralized here so every repository reads/writes
/// them the same way.
/// </summary>
internal static class SqliteDateTimeOffsetFormat
{
    public static string ToText(DateTimeOffset value) => value.ToString("O", CultureInfo.InvariantCulture);

    public static DateTimeOffset Parse(string value) =>
        DateTimeOffset.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
}
