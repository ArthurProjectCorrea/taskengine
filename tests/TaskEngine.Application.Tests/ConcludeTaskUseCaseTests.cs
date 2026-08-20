using TaskEngine.Application.Providers;
using TaskEngine.Application.Tasks;
using TaskEngine.Domain.Entities;
using TaskStatus = TaskEngine.Domain.Entities.TaskStatus;

namespace TaskEngine.Application.Tests;

public class ConcludeTaskUseCaseTests
{
    private static readonly DateTimeOffset Start = new(2026, 8, 17, 9, 0, 0, TimeSpan.Zero);

    private static ConcludeTaskUseCase CreateUseCase(
        FakeTaskRepository taskRepository,
        FakeWorkSessionRepository workSessionRepository,
        FakeProviderClientFactory? providerClientFactory = null,
        FakeAppSettingsStore? appSettingsStore = null) =>
        new(taskRepository, workSessionRepository, providerClientFactory ?? new FakeProviderClientFactory(), appSettingsStore ?? new FakeAppSettingsStore());

    [Fact]
    public async Task ExecuteAsync_WithSelectedActivities_ComputesHumanAndAiDurations()
    {
        var taskRepository = new FakeTaskRepository();
        var workSessionRepository = new FakeWorkSessionRepository();
        var task = TaskItem.Create("Write report");
        task.Start();
        await taskRepository.AddAsync(task, CancellationToken.None);

        var session = WorkSession.Start(task.Id, Start);
        session.RecordActivity(ActivitySource.Human, Start, Start.AddMinutes(30));
        session.RecordActivity(ActivitySource.Ai, Start.AddMinutes(30), Start.AddMinutes(45));
        session.End(Start.AddMinutes(45));
        await workSessionRepository.AddAsync(session, CancellationToken.None);

        var selectedIds = new HashSet<Guid> { session.Activities[0].Id, session.Activities[1].Id };
        var useCase = CreateUseCase(taskRepository, workSessionRepository);

        ConcludeTaskResult result = await useCase.ExecuteAsync(new ConcludeTaskRequest(task.Id, selectedIds));

        Assert.Equal(TimeSpan.FromMinutes(30), result.HumanDuration);
        Assert.Equal(TimeSpan.FromMinutes(15), result.AiDuration);
        Assert.Equal(TimeSpan.FromMinutes(45), result.TotalDuration);
        Assert.Equal(TaskStatus.Done.ToString(), result.Task.Status);
    }

    [Fact]
    public async Task ExecuteAsync_WithOverlappingHumanAndAiActivity_DoesNotDoubleCountTotalDuration()
    {
        var taskRepository = new FakeTaskRepository();
        var workSessionRepository = new FakeWorkSessionRepository();
        var task = TaskItem.Create("Write report");
        task.Start();
        await taskRepository.AddAsync(task, CancellationToken.None);

        var session = WorkSession.Start(task.Id, Start);
        session.RecordActivity(ActivitySource.Human, Start, Start.AddMinutes(30));
        session.RecordActivity(ActivitySource.Ai, Start.AddMinutes(20), Start.AddMinutes(40));
        session.End(Start.AddMinutes(40));
        await workSessionRepository.AddAsync(session, CancellationToken.None);

        var selectedIds = new HashSet<Guid> { session.Activities[0].Id, session.Activities[1].Id };
        var useCase = CreateUseCase(taskRepository, workSessionRepository);

        ConcludeTaskResult result = await useCase.ExecuteAsync(new ConcludeTaskRequest(task.Id, selectedIds));

        Assert.Equal(TimeSpan.FromMinutes(30), result.HumanDuration);
        Assert.Equal(TimeSpan.FromMinutes(20), result.AiDuration);
        // Union of 0-30 and 20-40 is 0-40 = 40 minutes, not the naive 50-minute sum (RN-007).
        Assert.Equal(TimeSpan.FromMinutes(40), result.TotalDuration);
    }

    [Fact]
    public async Task ExecuteAsync_ExcludesUnselectedActivitiesFromDurations()
    {
        var taskRepository = new FakeTaskRepository();
        var workSessionRepository = new FakeWorkSessionRepository();
        var task = TaskItem.Create("Write report");
        task.Start();
        await taskRepository.AddAsync(task, CancellationToken.None);

        var session = WorkSession.Start(task.Id, Start);
        session.RecordActivity(ActivitySource.Human, Start, Start.AddMinutes(30));
        session.RecordActivity(ActivitySource.Human, Start.AddMinutes(30), Start.AddMinutes(60));
        session.End(Start.AddMinutes(60));
        await workSessionRepository.AddAsync(session, CancellationToken.None);

        // Only the first activity is selected as belonging to the task.
        var selectedIds = new HashSet<Guid> { session.Activities[0].Id };
        var useCase = CreateUseCase(taskRepository, workSessionRepository);

        ConcludeTaskResult result = await useCase.ExecuteAsync(new ConcludeTaskRequest(task.Id, selectedIds));

        Assert.Equal(TimeSpan.FromMinutes(30), result.HumanDuration);
        Assert.Equal(TimeSpan.Zero, result.AiDuration);
    }

    [Fact]
    public async Task ExecuteAsync_WithNoSelection_ReturnsZeroDurationsAndStillConcludes()
    {
        var taskRepository = new FakeTaskRepository();
        var workSessionRepository = new FakeWorkSessionRepository();
        var task = TaskItem.Create("Write report");
        task.Start();
        await taskRepository.AddAsync(task, CancellationToken.None);

        var session = WorkSession.Start(task.Id, Start);
        session.RecordActivity(ActivitySource.Human, Start, Start.AddMinutes(30));
        session.End(Start.AddMinutes(30));
        await workSessionRepository.AddAsync(session, CancellationToken.None);

        var useCase = CreateUseCase(taskRepository, workSessionRepository);

        ConcludeTaskResult result = await useCase.ExecuteAsync(new ConcludeTaskRequest(task.Id, new HashSet<Guid>()));

        Assert.Equal(TimeSpan.Zero, result.HumanDuration);
        Assert.Equal(TimeSpan.Zero, result.TotalDuration);
        Assert.Equal(TaskStatus.Done.ToString(), result.Task.Status);
    }

    [Fact]
    public async Task ExecuteAsync_ExcludesPausePeriodsFromDurationsEvenIfSelected()
    {
        // RN-012: pause periods are excluded from the final calculation regardless of selection -
        // pause sessions never have activities in the first place (WorkSession.RecordActivity
        // rejects it), but this confirms the use case doesn't try to read them either.
        var taskRepository = new FakeTaskRepository();
        var workSessionRepository = new FakeWorkSessionRepository();
        var task = TaskItem.Create("Write report");
        task.Start();
        task.Pause();
        await taskRepository.AddAsync(task, CancellationToken.None);

        var activeSession = WorkSession.Start(task.Id, Start);
        activeSession.RecordActivity(ActivitySource.Human, Start, Start.AddMinutes(30));
        activeSession.End(Start.AddMinutes(30));
        await workSessionRepository.AddAsync(activeSession, CancellationToken.None);

        var pauseSession = WorkSession.Start(task.Id, Start.AddMinutes(30), WorkSessionType.Pause, WorkSessionOrigin.System);
        await workSessionRepository.AddAsync(pauseSession, CancellationToken.None);

        var selectedIds = new HashSet<Guid> { activeSession.Activities[0].Id };
        var useCase = CreateUseCase(taskRepository, workSessionRepository);

        ConcludeTaskResult result = await useCase.ExecuteAsync(new ConcludeTaskRequest(task.Id, selectedIds));

        Assert.Equal(TimeSpan.FromMinutes(30), result.HumanDuration);
        WorkSession closedPause = workSessionRepository.Sessions.Single(s => s.Id == pauseSession.Id);
        Assert.False(closedPause.IsOpen);
    }

    [Fact]
    public async Task ExecuteAsync_ClosesAnyOpenSession()
    {
        var taskRepository = new FakeTaskRepository();
        var workSessionRepository = new FakeWorkSessionRepository();
        var task = TaskItem.Create("Write report");
        task.Start();
        await taskRepository.AddAsync(task, CancellationToken.None);

        var session = WorkSession.Start(task.Id, Start);
        session.RecordActivity(ActivitySource.Human, Start, Start.AddMinutes(10));
        await workSessionRepository.AddAsync(session, CancellationToken.None);
        Assert.True(session.IsOpen);

        var useCase = CreateUseCase(taskRepository, workSessionRepository);
        await useCase.ExecuteAsync(new ConcludeTaskRequest(task.Id, new HashSet<Guid> { session.Activities[0].Id }));

        Assert.False(workSessionRepository.Sessions.Single(s => s.Id == session.Id).IsOpen);
    }

    [Fact]
    public async Task ExecuteAsync_PersistsTheActivitySelection()
    {
        var taskRepository = new FakeTaskRepository();
        var workSessionRepository = new FakeWorkSessionRepository();
        var task = TaskItem.Create("Write report");
        task.Start();
        await taskRepository.AddAsync(task, CancellationToken.None);

        var session = WorkSession.Start(task.Id, Start);
        session.RecordActivity(ActivitySource.Human, Start, Start.AddMinutes(10));
        session.RecordActivity(ActivitySource.Human, Start.AddMinutes(10), Start.AddMinutes(20));
        session.End(Start.AddMinutes(20));
        await workSessionRepository.AddAsync(session, CancellationToken.None);

        var keptId = session.Activities[0].Id;
        var useCase = CreateUseCase(taskRepository, workSessionRepository);
        await useCase.ExecuteAsync(new ConcludeTaskRequest(task.Id, new HashSet<Guid> { keptId }));

        WorkSession persisted = workSessionRepository.Sessions.Single(s => s.Id == session.Id);
        Assert.True(persisted.Activities.Single(a => a.Id == keptId).SelectedAtConclusion);
        Assert.False(persisted.Activities.Single(a => a.Id != keptId).SelectedAtConclusion);
    }

    [Fact]
    public async Task ExecuteAsync_WithUnknownTask_Throws()
    {
        var taskRepository = new FakeTaskRepository();
        var workSessionRepository = new FakeWorkSessionRepository();
        var useCase = CreateUseCase(taskRepository, workSessionRepository);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => useCase.ExecuteAsync(new ConcludeTaskRequest(Guid.NewGuid(), new HashSet<Guid>())));
    }

    [Fact]
    public async Task ExecuteAsync_WithAlreadyDoneTask_ThrowsAndDoesNotMutateSessions()
    {
        var taskRepository = new FakeTaskRepository();
        var workSessionRepository = new FakeWorkSessionRepository();
        var task = TaskItem.Create("Write report");
        task.Start();
        task.Complete();
        await taskRepository.AddAsync(task, CancellationToken.None);

        var useCase = CreateUseCase(taskRepository, workSessionRepository);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => useCase.ExecuteAsync(new ConcludeTaskRequest(task.Id, new HashSet<Guid>())));
        Assert.Empty(workSessionRepository.Sessions);
    }

    [Fact]
    public async Task ExecuteAsync_WithProviderLinkedTask_ReportsCompletionAndCompletesOnline()
    {
        var taskRepository = new FakeTaskRepository();
        var workSessionRepository = new FakeWorkSessionRepository();
        var task = TaskItem.Create("Write report", providerTaskId: "gh-1", providerId: "github");
        task.Start();
        await taskRepository.AddAsync(task, CancellationToken.None);

        var session = WorkSession.Start(task.Id, Start);
        session.RecordActivity(ActivitySource.Human, Start, Start.AddMinutes(30));
        session.End(Start.AddMinutes(30));
        await workSessionRepository.AddAsync(session, CancellationToken.None);

        var providerClientFactory = new FakeProviderClientFactory();
        var useCase = CreateUseCase(taskRepository, workSessionRepository, providerClientFactory);

        ConcludeTaskResult result = await useCase.ExecuteAsync(
            new ConcludeTaskRequest(task.Id, new HashSet<Guid> { session.Activities[0].Id }));

        Assert.Equal(TaskStatus.Done.ToString(), result.Task.Status);
        Assert.Equal("gh-1", providerClientFactory.ClientToReturn.LastReportedCompletionReference?.ExternalId);
        Assert.Equal(TimeSpan.FromMinutes(30), providerClientFactory.ClientToReturn.LastReportedCompletionDuration);
    }

    [Fact]
    public async Task ExecuteAsync_WithProviderTaskButPushFails_CompletesOfflinePendingSync()
    {
        var taskRepository = new FakeTaskRepository();
        var workSessionRepository = new FakeWorkSessionRepository();
        var task = TaskItem.Create("Write report", providerTaskId: "gh-1", providerId: "github");
        task.Start();
        await taskRepository.AddAsync(task, CancellationToken.None);

        var providerClientFactory = new FakeProviderClientFactory();
        providerClientFactory.ClientToReturn.ReportCompletionFailure = new HttpRequestException("No connectivity.");
        var useCase = CreateUseCase(taskRepository, workSessionRepository, providerClientFactory);

        ConcludeTaskResult result = await useCase.ExecuteAsync(new ConcludeTaskRequest(task.Id, new HashSet<Guid>()));

        Assert.Equal(TaskStatus.DonePendingSync.ToString(), result.Task.Status);
    }

    [Fact]
    public async Task ExecuteAsync_WithoutProviderLink_NeverCallsTheProviderClientFactory()
    {
        var taskRepository = new FakeTaskRepository();
        var workSessionRepository = new FakeWorkSessionRepository();
        var task = TaskItem.Create("Local-only task");
        task.Start();
        await taskRepository.AddAsync(task, CancellationToken.None);

        var providerClientFactory = new FakeProviderClientFactory();
        var useCase = CreateUseCase(taskRepository, workSessionRepository, providerClientFactory);

        await useCase.ExecuteAsync(new ConcludeTaskRequest(task.Id, new HashSet<Guid>()));

        Assert.Null(providerClientFactory.LastRequestedProviderId);
    }

    [Fact]
    public async Task ExecuteAsync_WhenProviderIsAlreadyFrozen_ThrowsAndLeavesTaskAndSessionsUntouched()
    {
        var taskRepository = new FakeTaskRepository();
        var workSessionRepository = new FakeWorkSessionRepository();
        var task = TaskItem.Create("Write report", providerTaskId: "gh-1", providerId: "github");
        task.Start();
        await taskRepository.AddAsync(task, CancellationToken.None);

        var session = WorkSession.Start(task.Id, Start);
        session.RecordActivity(ActivitySource.Human, Start, Start.AddMinutes(30));
        await workSessionRepository.AddAsync(session, CancellationToken.None);

        var appSettingsStore = new FakeAppSettingsStore();
        await appSettingsStore.SetAsync(ProviderSettingsKeys.Frozen("github"), "true", CancellationToken.None);
        var providerClientFactory = new FakeProviderClientFactory();
        var useCase = CreateUseCase(taskRepository, workSessionRepository, providerClientFactory, appSettingsStore);

        await Assert.ThrowsAsync<ProviderFrozenException>(
            () => useCase.ExecuteAsync(new ConcludeTaskRequest(task.Id, new HashSet<Guid> { session.Activities[0].Id })));

        Assert.Equal(TaskStatus.InProgress, Assert.Single(taskRepository.Tasks).Status);
        Assert.True(workSessionRepository.Sessions.Single().IsOpen);
        Assert.False(workSessionRepository.Sessions.Single().Activities.Single().SelectedAtConclusion);
        Assert.Null(providerClientFactory.LastRequestedProviderId);
    }

    [Fact]
    public async Task ExecuteAsync_WhenProviderFreezesDuringThePush_ThrowsWithoutFallingBackToOffline()
    {
        var taskRepository = new FakeTaskRepository();
        var workSessionRepository = new FakeWorkSessionRepository();
        var task = TaskItem.Create("Write report", providerTaskId: "gh-1", providerId: "github");
        task.Start();
        await taskRepository.AddAsync(task, CancellationToken.None);

        var providerClientFactory = new FakeProviderClientFactory();
        providerClientFactory.ClientToReturn.ReportCompletionFailure = new ProviderFrozenException("github");
        var useCase = CreateUseCase(taskRepository, workSessionRepository, providerClientFactory);

        await Assert.ThrowsAsync<ProviderFrozenException>(
            () => useCase.ExecuteAsync(new ConcludeTaskRequest(task.Id, new HashSet<Guid>())));

        Assert.Equal(TaskStatus.InProgress, Assert.Single(taskRepository.Tasks).Status);
    }
}
