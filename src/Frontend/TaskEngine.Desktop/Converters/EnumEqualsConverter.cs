using System.Globalization;

namespace TaskEngine.Desktop.Converters;

/// <summary>
/// Generic <see cref="IValueConverter"/> that compares an enum-valued binding (e.g.
/// <c>OnboardingViewModel.State</c>) against a <c>ConverterParameter</c> given as a string,
/// avoiding one bespoke boolean converter per enum value. No third-party MVVM/converter
/// library involved — this is the same hand-rolled approach as the rest of <c>Mvvm/</c>.
/// </summary>
public sealed class EnumEqualsConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is null || parameter is null)
        {
            return false;
        }

        return string.Equals(value.ToString(), parameter.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException($"{nameof(EnumEqualsConverter)} only supports one-way binding.");
    }
}
