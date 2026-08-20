namespace TaskEngine.Desktop.ViewModels.Navigation;

/// <summary>
/// Optional hook (issue #53) letting a section's view model receive the parameter passed to
/// <see cref="INavigationService.NavigateTo"/>, if any - e.g. <c>DetalhesTarefaViewModel</c>
/// receiving the task id to load. Deliberately minimal (a single method, no generic
/// navigation/routing framework) since only one screen needs it today; the Desktop-only
/// <c>SectionViewFactory</c> calls this right after resolving a section's View, if that View's
/// <c>BindingContext</c> implements this interface - every other section is unaffected.
/// </summary>
public interface INavigationAware
{
    void ApplyParameter(object? parameter);
}
