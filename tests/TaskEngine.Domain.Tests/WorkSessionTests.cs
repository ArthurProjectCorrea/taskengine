using TaskEngine.Domain.Entities;

namespace TaskEngine.Domain.Tests;

public class WorkSessionTests
{
    private static readonly DateTimeOffset Start = new(2026, 8, 17, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Start_CreatesAnOpenSession()
    {
        var session = WorkSession.Start(Guid.NewGuid(), Start);

        Assert.True(session.IsOpen);
        Assert.Null(session.EndedAt);
    }

    [Fact]
    public void End_ClosesTheSession()
    {
        var session = WorkSession.Start(Guid.NewGuid(), Start);

        session.End(Start.AddHours(1));

        Assert.False(session.IsOpen);
        Assert.Equal(Start.AddHours(1), session.EndedAt);
    }

    [Fact]
    public void End_ThrowsWhenAlreadyClosed()
    {
        var session = WorkSession.Start(Guid.NewGuid(), Start);
        session.End(Start.AddHours(1));

        Assert.Throws<InvalidOperationException>(() => session.End(Start.AddHours(2)));
    }

    [Fact]
    public void End_ThrowsWhenEndedAtIsBeforeStartedAt()
    {
        var session = WorkSession.Start(Guid.NewGuid(), Start);

        Assert.Throws<ArgumentException>(() => session.End(Start.AddMinutes(-1)));
    }

    [Fact]
    public void RecordActivity_ThrowsWhenSessionIsClosed()
    {
        var session = WorkSession.Start(Guid.NewGuid(), Start);
        session.End(Start.AddHours(1));

        Assert.Throws<InvalidOperationException>(
            () => session.RecordActivity(ActivitySource.Human, Start, Start.AddMinutes(10)));
    }

    [Fact]
    public void RecordActivity_ThrowsWhenSessionIsAPause()
    {
        var session = WorkSession.Start(Guid.NewGuid(), Start, WorkSessionType.Pause, WorkSessionOrigin.System);

        Assert.Throws<InvalidOperationException>(
            () => session.RecordActivity(ActivitySource.Human, Start, Start.AddMinutes(10)));
    }

    [Fact]
    public void Start_DefaultsToActiveTypeAndSystemOrigin()
    {
        var session = WorkSession.Start(Guid.NewGuid(), Start);

        Assert.Equal(WorkSessionType.Active, session.Type);
        Assert.Equal(WorkSessionOrigin.System, session.Origin);
    }

    [Fact]
    public void Start_WithExplicitTypeAndOrigin_SetsThem()
    {
        var session = WorkSession.Start(Guid.NewGuid(), Start, WorkSessionType.Pause, WorkSessionOrigin.Provider);

        Assert.Equal(WorkSessionType.Pause, session.Type);
        Assert.Equal(WorkSessionOrigin.Provider, session.Origin);
    }

    [Fact]
    public void Restore_RoundTripsTypeAndOrigin()
    {
        var id = Guid.NewGuid();
        var taskId = Guid.NewGuid();

        var session = WorkSession.Restore(id, taskId, Start, null, [], WorkSessionType.Pause, WorkSessionOrigin.Provider);

        Assert.Equal(WorkSessionType.Pause, session.Type);
        Assert.Equal(WorkSessionOrigin.Provider, session.Origin);
    }

    [Fact]
    public void RecordActivity_WithTypeAndPath_SetsThemOnTheStoredActivity()
    {
        var session = WorkSession.Start(Guid.NewGuid(), Start);

        session.RecordActivity(
            ActivitySource.Human, Start, Start.AddMinutes(10), ActivityItemType.Browser, "https://example.com");

        ActivityInterval activity = Assert.Single(session.Activities);
        Assert.Equal(ActivityItemType.Browser, activity.Type);
        Assert.Equal("https://example.com", activity.Path);
    }

    [Fact]
    public void ApplyActivitySelection_MarksOnlySelectedActivitiesAsSelected()
    {
        var session = WorkSession.Start(Guid.NewGuid(), Start);
        session.RecordActivity(ActivitySource.Human, Start, Start.AddMinutes(10));
        session.RecordActivity(ActivitySource.Ai, Start.AddMinutes(10), Start.AddMinutes(20));
        var keptId = session.Activities[0].Id;

        session.ApplyActivitySelection(new HashSet<Guid> { keptId });

        Assert.True(session.Activities[0].SelectedAtConclusion);
        Assert.False(session.Activities[1].SelectedAtConclusion);
    }

    [Fact]
    public void ApplyActivitySelection_WithEmptySet_ClearsAnyPreviousSelection()
    {
        var session = WorkSession.Start(Guid.NewGuid(), Start);
        session.RecordActivity(ActivitySource.Human, Start, Start.AddMinutes(10));
        session.ApplyActivitySelection(new HashSet<Guid> { session.Activities[0].Id });

        session.ApplyActivitySelection(new HashSet<Guid>());

        Assert.False(session.Activities[0].SelectedAtConclusion);
    }

    [Fact]
    public void HumanAndAiDuration_SumOnlyTheirOwnSourceIntervals()
    {
        var session = WorkSession.Start(Guid.NewGuid(), Start);

        session.RecordActivity(ActivitySource.Human, Start, Start.AddMinutes(10));
        session.RecordActivity(ActivitySource.Human, Start.AddMinutes(10), Start.AddMinutes(25));
        session.RecordActivity(ActivitySource.Ai, Start.AddMinutes(25), Start.AddMinutes(30));

        Assert.Equal(TimeSpan.FromMinutes(25), session.HumanDuration);
        Assert.Equal(TimeSpan.FromMinutes(5), session.AiDuration);
        Assert.Equal(3, session.Activities.Count);
    }
}
