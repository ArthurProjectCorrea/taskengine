using TaskEngine.Desktop.ViewModels.Navigation;

namespace TaskEngine.Desktop.Tests;

/// <summary>Hand-written in-memory fake for <see cref="INavigationService"/>, no mocking library.</summary>
public sealed class FakeNavigationService : INavigationService
{
    public AppSection CurrentSection { get; private set; } = AppSection.Dashboard;

    public object? CurrentParameter { get; private set; }

    public event Action<AppSection>? SectionChanged;

    /// <summary>Every call recorded, in order - lets a test assert both the final state and exactly how NavigateTo was invoked.</summary>
    public List<(AppSection Section, object? Parameter)> Calls { get; } = [];

    public void NavigateTo(AppSection section, object? parameter = null)
    {
        Calls.Add((section, parameter));
        CurrentSection = section;
        CurrentParameter = parameter;
        SectionChanged?.Invoke(section);
    }
}
