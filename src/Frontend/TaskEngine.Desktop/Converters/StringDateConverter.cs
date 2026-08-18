using System.Globalization;

namespace TaskEngine.Desktop.Converters;

/// <summary>
/// Bridges <c>TaskFieldInputViewModel.Value</c> (a <see cref="string"/>, since it's sent to the
/// provider as text regardless of field type) and <c>DatePicker.Date</c> (a <see cref="DateTime"/>),
/// for <c>ProviderFieldType.Date</c> fields in <c>CreateTaskPage.xaml</c>'s field template.
/// Round-trips through ISO 8601 ("yyyy-MM-dd") so the value sent to the backend is unambiguous
/// regardless of the user's locale.
/// </summary>
public sealed class StringDateConverter : IValueConverter
{
    private const string IsoDateFormat = "yyyy-MM-dd";

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string text && DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
        {
            return parsed;
        }

        return DateTime.Today;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is DateTime date)
        {
            return date.ToString(IsoDateFormat, CultureInfo.InvariantCulture);
        }

        return null;
    }
}
