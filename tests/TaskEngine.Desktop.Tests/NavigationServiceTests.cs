using TaskEngine.Desktop.Navigation;
using TaskEngine.Desktop.ViewModels.Navigation;

namespace TaskEngine.Desktop.Tests;

/// <summary>
/// Unit tests for the real <see cref="NavigationService"/> (issue #53's parameter-passing
/// extension) - compiled directly into this test assembly via a linked <c>Compile</c> item (see
/// TaskEngine.Desktop.Tests.csproj) rather than a <c>ProjectReference</c> to TaskEngine.Desktop,
/// which is a MAUI SingleProject that cannot be referenced from a plain xUnit test project (see
/// that csproj's own comment on the existing ViewModels reference). NavigationService itself has
/// no MAUI dependency, so this is safe and lets these tests exercise the actual guard-clause logic
/// instead of only <see cref="FakeNavigationService"/>.
/// </summary>
public class NavigationServiceTests
{
    [Fact]
    public void NavigateTo_DifferentSection_UpdatesCurrentSection_AndRaisesSectionChanged()
    {
        var sut = new NavigationService();
        AppSection? raised = null;
        sut.SectionChanged += s => raised = s;

        sut.NavigateTo(AppSection.Tarefas);

        Assert.Equal(AppSection.Tarefas, sut.CurrentSection);
        Assert.Equal(AppSection.Tarefas, raised);
    }

    [Fact]
    public void NavigateTo_SameSection_NoParameter_IsANoOp()
    {
        var sut = new NavigationService(); // starts at Dashboard, CurrentParameter already null
        var raisedCount = 0;
        sut.SectionChanged += _ => raisedCount++;

        sut.NavigateTo(AppSection.Dashboard);

        Assert.Equal(0, raisedCount);
    }

    [Fact]
    public void NavigateTo_WithParameter_ExposesItViaCurrentParameter()
    {
        var sut = new NavigationService();
        var taskId = Guid.NewGuid();

        sut.NavigateTo(AppSection.DetalhesTarefa, taskId);

        Assert.Equal(AppSection.DetalhesTarefa, sut.CurrentSection);
        Assert.Equal(taskId, sut.CurrentParameter);
    }

    [Fact]
    public void NavigateTo_SameSection_DifferentParameter_StillRaisesSectionChanged()
    {
        // issue #53: opening Detalhes for a second task while already viewing Detalhes for a
        // first one must still swap the resolved view/reload data - a section-only guard (the
        // pre-#53 behavior) would silently swallow this second navigation.
        var sut = new NavigationService();
        var firstTaskId = Guid.NewGuid();
        var secondTaskId = Guid.NewGuid();
        sut.NavigateTo(AppSection.DetalhesTarefa, firstTaskId);

        var raisedCount = 0;
        sut.SectionChanged += _ => raisedCount++;
        sut.NavigateTo(AppSection.DetalhesTarefa, secondTaskId);

        Assert.Equal(1, raisedCount);
        Assert.Equal(secondTaskId, sut.CurrentParameter);
    }

    [Fact]
    public void NavigateTo_SameSection_SameParameter_IsANoOp()
    {
        var sut = new NavigationService();
        var taskId = Guid.NewGuid();
        sut.NavigateTo(AppSection.DetalhesTarefa, taskId);

        var raisedCount = 0;
        sut.SectionChanged += _ => raisedCount++;
        sut.NavigateTo(AppSection.DetalhesTarefa, taskId);

        Assert.Equal(0, raisedCount);
        Assert.Equal(taskId, sut.CurrentParameter);
    }

    [Fact]
    public void NavigateTo_LeavingASectionWithAParameter_ForOneWithout_ClearsCurrentParameter()
    {
        var sut = new NavigationService();
        sut.NavigateTo(AppSection.DetalhesTarefa, Guid.NewGuid());

        sut.NavigateTo(AppSection.Tarefas);

        Assert.Null(sut.CurrentParameter);
    }
}
