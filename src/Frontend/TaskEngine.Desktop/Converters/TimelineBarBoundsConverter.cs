using System.Globalization;
using TaskEngine.Desktop.ViewModels;

namespace TaskEngine.Desktop.Converters;

/// <summary>
/// Converts a whole <see cref="TimelineBarItem"/> into the <see cref="Rect"/> its <c>Border</c>
/// binds to via <c>AbsoluteLayout.LayoutBounds</c> (with <c>XProportional,WidthProportional</c>
/// flags, set once in the view - not bound here) - RelatorioPage's simplified Gantt-like timeline
/// (plan item 9, RF-010/CA-010.1, ERS-Tarefas.md). Converts the whole item (not
/// <see cref="TimelineBarItem.LeftFraction"/>/<see cref="TimelineBarItem.WidthFraction"/>
/// separately) because .NET MAUI XAML has no built-in way to compose two independent bindings into
/// a single <see cref="Rect"/>-typed attached property without a <c>MultiBinding</c> - converting
/// the item as a whole avoids that ceremony for a single-purpose conversion like this one. The Y/height
/// (4/34) are fixed - only X/width vary per item, matching the "44px lane, small vertical padding"
/// layout the view's <c>AbsoluteLayout</c> lane container uses.
/// </summary>
public sealed class TimelineBarBoundsConverter : IValueConverter
{
    private const double Top = 4;
    private const double Height = 34;

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not TimelineBarItem bar)
        {
            return new Rect(0, Top, 0, Height);
        }

        return new Rect(bar.LeftFraction, Top, bar.WidthFraction, Height);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException($"{nameof(TimelineBarBoundsConverter)} only supports one-way binding.");
    }
}
