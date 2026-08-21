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
    public void AttributeSelectedActivity_AddsItMarkedSelected()
    {
        var session = WorkSession.Start(Guid.NewGuid(), Start);
        var monitored = new ActivityInterval(ActivitySource.Human, Start, Start.AddMinutes(10), ActivityItemType.File, "src/a.cs");

        session.AttributeSelectedActivity(monitored);

        ActivityInterval attributed = Assert.Single(session.Activities);
        Assert.Equal(monitored.Id, attributed.Id);
        Assert.True(attributed.SelectedAtConclusion);
    }

    [Fact]
    public void AttributeSelectedActivity_WorksOnAnAlreadyClosedActiveSession()
    {
        // Conclusion attributes activity from every past Active session, not just a still-open one
        // (e.g. an earlier work period before a pause/resume cycle).
        var session = WorkSession.Start(Guid.NewGuid(), Start);
        session.End(Start.AddMinutes(30));
        var monitored = new ActivityInterval(ActivitySource.Human, Start, Start.AddMinutes(10), ActivityItemType.File, "src/a.cs");

        session.AttributeSelectedActivity(monitored);

        Assert.Single(session.Activities);
    }

    [Fact]
    public void AttributeSelectedActivity_ThrowsOnAPauseSession()
    {
        var session = WorkSession.Start(Guid.NewGuid(), Start, WorkSessionType.Pause, WorkSessionOrigin.System);
        var monitored = new ActivityInterval(ActivitySource.Human, Start, Start.AddMinutes(10), ActivityItemType.File, "src/a.cs");

        Assert.Throws<InvalidOperationException>(() => session.AttributeSelectedActivity(monitored));
    }

    [Fact]
    public void AttributeSelectedActivity_CalledTwiceWithSameId_DoesNotDuplicate()
    {
        var session = WorkSession.Start(Guid.NewGuid(), Start);
        var monitored = new ActivityInterval(ActivitySource.Human, Start, Start.AddMinutes(10), ActivityItemType.File, "src/a.cs");

        session.AttributeSelectedActivity(monitored);
        session.AttributeSelectedActivity(monitored);

        Assert.Single(session.Activities);
    }

    /// <summary>
    /// Regression test: a monitored activity found via an overlap query (RF-004/CA-004.1) can span
    /// far outside the session's own window (e.g. a file also touched days before this task even
    /// started). Attributing it unclipped used to credit the session with the activity's full
    /// lifetime instead of just the portion that actually happened during this session - a 2-hour
    /// task could end up "concluding" with 5 days of duration if it selected a long-lived file.
    /// </summary>
    [Fact]
    public void AttributeSelectedActivity_ActivityStartsBeforeSession_ClipsToSessionStart()
    {
        var session = WorkSession.Start(Guid.NewGuid(), Start);
        session.End(Start.AddHours(2));
        var monitored = new ActivityInterval(
            ActivitySource.Human, Start.AddDays(-5), Start.AddHours(1), ActivityItemType.File, "src/a.cs");

        session.AttributeSelectedActivity(monitored);

        ActivityInterval attributed = Assert.Single(session.Activities);
        Assert.Equal(Start, attributed.StartedAt);
        Assert.Equal(Start.AddHours(1), attributed.EndedAt);
        Assert.Equal(monitored.Id, attributed.Id);
    }

    [Fact]
    public void AttributeSelectedActivity_ActivityEndsAfterSession_ClipsToSessionEnd()
    {
        var session = WorkSession.Start(Guid.NewGuid(), Start);
        session.End(Start.AddHours(2));
        var monitored = new ActivityInterval(
            ActivitySource.Human, Start.AddMinutes(30), Start.AddDays(5), ActivityItemType.File, "src/a.cs");

        session.AttributeSelectedActivity(monitored);

        ActivityInterval attributed = Assert.Single(session.Activities);
        Assert.Equal(Start.AddMinutes(30), attributed.StartedAt);
        Assert.Equal(Start.AddHours(2), attributed.EndedAt);
    }

    [Fact]
    public void AttributeSelectedActivity_ActivityEntirelyWithinSession_IsNotClipped()
    {
        var session = WorkSession.Start(Guid.NewGuid(), Start);
        session.End(Start.AddHours(2));
        var monitored = new ActivityInterval(
            ActivitySource.Human, Start.AddMinutes(10), Start.AddMinutes(40), ActivityItemType.File, "src/a.cs");

        session.AttributeSelectedActivity(monitored);

        ActivityInterval attributed = Assert.Single(session.Activities);
        Assert.Equal(monitored.StartedAt, attributed.StartedAt);
        Assert.Equal(monitored.EndedAt, attributed.EndedAt);
    }

    [Fact]
    public void AttributeSelectedActivity_OpenSession_ClipsToNowNotToDefault()
    {
        var session = WorkSession.Start(Guid.NewGuid(), Start);
        var now = Start.AddMinutes(20);
        var monitored = new ActivityInterval(
            ActivitySource.Human, Start.AddMinutes(-90), now.AddDays(1), ActivityItemType.File, "src/a.cs");

        session.AttributeSelectedActivity(monitored);

        ActivityInterval attributed = Assert.Single(session.Activities);
        Assert.Equal(Start, attributed.StartedAt);
        Assert.True(attributed.EndedAt <= DateTimeOffset.UtcNow);
        Assert.True(attributed.EndedAt >= now);
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
