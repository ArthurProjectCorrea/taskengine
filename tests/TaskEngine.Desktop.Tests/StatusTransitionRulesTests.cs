using TaskEngine.Desktop.ViewModels;
using DomainTaskStatus = TaskEngine.Domain.Entities.TaskStatus;

namespace TaskEngine.Desktop.Tests;

public class StatusTransitionRulesTests
{
    [Fact]
    public void GetValidTransitions_ToDo_OnlyOffersStart()
    {
        Assert.Equal([DomainTaskStatus.InProgress], StatusTransitionRules.GetValidTransitions(DomainTaskStatus.ToDo));
    }

    [Fact]
    public void GetValidTransitions_InProgress_OffersPauseAndComplete()
    {
        Assert.Equal(
            [DomainTaskStatus.Paused, DomainTaskStatus.Done],
            StatusTransitionRules.GetValidTransitions(DomainTaskStatus.InProgress));
    }

    [Fact]
    public void GetValidTransitions_Paused_OnlyOffersResume()
    {
        Assert.Equal([DomainTaskStatus.InProgress], StatusTransitionRules.GetValidTransitions(DomainTaskStatus.Paused));
    }

    [Fact]
    public void GetValidTransitions_Done_IsTerminal_NoTransitionsOffered()
    {
        Assert.Empty(StatusTransitionRules.GetValidTransitions(DomainTaskStatus.Done));
    }

    [Fact]
    public void GetValidTransitions_DonePendingSync_IsTerminal_NoTransitionsOffered()
    {
        // Even though the domain eventually moves this to Done once the pending sync succeeds,
        // that transition is driven by background sync, not by the user - so the UI must not
        // offer it as a pickable option.
        Assert.Empty(StatusTransitionRules.GetValidTransitions(DomainTaskStatus.DonePendingSync));
    }

    [Theory]
    [InlineData(DomainTaskStatus.ToDo, DomainTaskStatus.InProgress, "Iniciar")]
    [InlineData(DomainTaskStatus.InProgress, DomainTaskStatus.Paused, "Pausar")]
    [InlineData(DomainTaskStatus.InProgress, DomainTaskStatus.Done, "Concluir")]
    [InlineData(DomainTaskStatus.Paused, DomainTaskStatus.InProgress, "Retomar")]
    public void GetTransitionActionLabel_ReturnsExpectedActionVerb(DomainTaskStatus from, DomainTaskStatus to, string expectedLabel)
    {
        Assert.Equal(expectedLabel, StatusTransitionRules.GetTransitionActionLabel(from, to));
    }

    [Theory]
    [InlineData(DomainTaskStatus.ToDo, "A Fazer")]
    [InlineData(DomainTaskStatus.InProgress, "Em Andamento")]
    [InlineData(DomainTaskStatus.Paused, "Pausado")]
    [InlineData(DomainTaskStatus.Done, "Concluído")]
    [InlineData(DomainTaskStatus.DonePendingSync, "Concluído (sincronização pendente)")]
    public void GetStatusLabel_ReturnsExpectedDisplayText(DomainTaskStatus status, string expectedLabel)
    {
        Assert.Equal(expectedLabel, StatusTransitionRules.GetStatusLabel(status));
    }
}
