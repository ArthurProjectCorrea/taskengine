using TaskEngine.Application.Providers;
using TaskEngine.Desktop.ViewModels;

namespace TaskEngine.Desktop.Views;

/// <summary>
/// Picks which <see cref="DataTemplate"/> (defined as page-level resources in
/// <c>CreateTaskPage.xaml</c>) renders a given dynamic field, based on its
/// <see cref="ProviderFieldType"/> - this is what lets one <c>CollectionView</c> render an
/// Entry/DatePicker/Picker per row without any if/else in code-behind, keeping the view a pure
/// presentation layer per project convention.
/// </summary>
public sealed class TaskFieldTemplateSelector : DataTemplateSelector
{
    public DataTemplate? TextTemplate { get; set; }

    public DataTemplate? NumberTemplate { get; set; }

    public DataTemplate? DateTemplate { get; set; }

    public DataTemplate? SingleSelectTemplate { get; set; }

    protected override DataTemplate OnSelectTemplate(object item, BindableObject container)
    {
        var field = (TaskFieldInputViewModel)item;

        return field.Type switch
        {
            ProviderFieldType.Number => NumberTemplate ?? throw MissingTemplate(nameof(NumberTemplate)),
            ProviderFieldType.Date => DateTemplate ?? throw MissingTemplate(nameof(DateTemplate)),
            ProviderFieldType.SingleSelect => SingleSelectTemplate ?? throw MissingTemplate(nameof(SingleSelectTemplate)),
            _ => TextTemplate ?? throw MissingTemplate(nameof(TextTemplate)),
        };
    }

    private static InvalidOperationException MissingTemplate(string propertyName) =>
        new($"{nameof(TaskFieldTemplateSelector)}.{propertyName} was not set - check CreateTaskPage.xaml's resource wiring.");
}
