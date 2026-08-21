using System.Globalization;

namespace TaskEngine.Desktop.Converters;

/// <summary>
/// Generic <see cref="IValueConverter"/> that returns whether the bound value is non-null (or the
/// opposite, when <c>ConverterParameter=Invert</c> is given). Used by <c>OnboardingPage.xaml</c> to
/// switch between a provider's real brand icon (<see cref="ViewModels.ProviderOption.IconFileName"/>)
/// and a text-initial fallback when no icon asset is registered yet for that provider - avoids a
/// bespoke bool converter per nullable-string binding.
/// </summary>
public sealed class IsNotNullConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var isNotNull = value is not null;
        var invert = string.Equals(parameter as string, "Invert", StringComparison.OrdinalIgnoreCase);
        return invert ? !isNotNull : isNotNull;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException($"{nameof(IsNotNullConverter)} only supports one-way binding.");
    }
}
