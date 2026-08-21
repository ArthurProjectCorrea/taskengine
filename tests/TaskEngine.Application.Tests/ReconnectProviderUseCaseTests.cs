using TaskEngine.Application.Providers;
using TaskEngine.Application.Tasks;
using TaskEngine.Application.WorkSessions;
using TaskEngine.Domain.Entities;
using TaskStatus = TaskEngine.Domain.Entities.TaskStatus;

namespace TaskEngine.Application.Tests;

public class ReconnectProviderUseCaseTests
{
    private static ReconnectProviderUseCase CreateUseCase(
        FakeTaskRepository taskRepository,
        FakeProviderClientFactory providerClientFactory,
        FakeAppSettingsStore appSettingsStore,
        FakeWorkSessionRepository? workSessionRepository = null)
    {
        workSessionRepository ??= new FakeWorkSessionRepository();
        var syncTasksUseCase = new SyncTasksUseCase(
            taskRepository,
            providerClientFactory,
            appSettingsStore,
            new PauseWorkSessionUseCase(taskRepository, workSessionRepository),
            new StartWorkSessionUseCase(taskRepository, workSessionRepository),
            new EndWorkSessionUseCase(taskRepository, workSessionRepository));

        return new ReconnectProviderUseCase(
            appSettingsStore, taskRepository, workSessionRepository, providerClientFactory, syncTasksUseCase);
    }

    [Fact]
    public async Task ExecuteAsync_UnfreezesTheProvider()
    {
        var taskRepository = new FakeTaskRepository();
        var providerClientFactory = new FakeProviderClientFactory();
        var appSettingsStore = new FakeAppSettingsStore();
        await appSettingsStore.SetAsync(ProviderSettingsKeys.Frozen("github"), "true", CancellationToken.None);
        var useCase = CreateUseCase(taskRepository, providerClientFactory, appSettingsStore);

        await useCase.ExecuteAsync("github");

        Assert.Null(await appSettingsStore.GetAsync(ProviderSettingsKeys.Frozen("github"), CancellationToken.None));
    }

    [Fact]
    public async Task ExecuteAsync_PushesPendingCompletionsAndMarksThemSynced()
    {
        var taskRepository = new FakeTaskRepository();
        var task = TaskItem.Create("Write report", providerTaskId: "gh-1", providerId: "github");
        task.Start();
        task.CompleteOffline();
        await taskRepository.AddAsync(task, CancellationToken.None);

        var providerClientFactory = new FakeProviderClientFactory();
        var appSettingsStore = new FakeAppSettingsStore();
        var useCase = CreateUseCase(taskRepository, providerClientFactory, appSettingsStore);

        await useCase.ExecuteAsync("github");

        TaskItem persisted = Assert.Single(taskRepository.Tasks);
        Assert.Equal(TaskStatus.Done, persisted.Status);
    }

    [Fact]
    public async Task ExecuteAsync_PushesTheFinalTimeAlongsideStatus()
    {
        // CA-014.2: reconnecting must send the status *and* the final time computed at conclusion,
        // not status only.
        var taskRepository = new FakeTaskRepository();
        var task = TaskItem.Create("Write report", providerTaskId: "gh-1", providerId: "github");
        task.Start();
        await taskRepository.AddAsync(task, CancellationToken.None);

        var start = new DateTimeOffset(2026, 8, 17, 9, 0, 0, TimeSpan.Zero);
        var session = WorkSession.Start(task.Id, start);
        session.RecordActivity(ActivitySource.Human, start, start.AddMinutes(30));
        session.ApplyActivitySelection(new HashSet<Guid> { session.Activities[0].Id });
        session.End(start.AddMinutes(30));

        task.CompleteOffline();
        await taskRepository.UpdateAsync(task, CancellationToken.None);

        var workSessionRepository = new FakeWorkSessionRepository();
        await workSessionRepository.AddAsync(session, CancellationToken.None);

        var providerClientFactory = new FakeProviderClientFactory();
        var appSettingsStore = new FakeAppSettingsStore();
        var useCase = CreateUseCase(taskRepository, providerClientFactory, appSettingsStore, workSessionRepository);

        await useCase.ExecuteAsync("github");

        Assert.Equal("gh-1", providerClientFactory.ClientToReturn.LastReportedCompletionReference?.ExternalId);
        Assert.Equal(TimeSpan.FromMinutes(30), providerClientFactory.ClientToReturn.LastReportedCompletionDuration);
        Assert.Equal(TaskStatus.Done, Assert.Single(taskRepository.Tasks).Status);
    }

    [Fact]
    public async Task ExecuteAsync_IgnoresPendingTasksFromOtherProviders()
    {
        var taskRepository = new FakeTaskRepository();
        var task = TaskItem.Create("Other provider task", providerTaskId: "jira-1", providerId: "jira");
        task.Start();
        task.CompleteOffline();
        await taskRepository.AddAsync(task, CancellationToken.None);

        var providerClientFactory = new FakeProviderClientFactory();
        var appSettingsStore = new FakeAppSettingsStore();
        var useCase = CreateUseCase(taskRepository, providerClientFactory, appSettingsStore);

        await useCase.ExecuteAsync("github");

        Assert.Equal(TaskStatus.DonePendingSync, Assert.Single(taskRepository.Tasks).Status);
    }

    [Fact]
    public async Task ExecuteAsync_RunsANormalSyncAfterwards()
    {
        var taskRepository = new FakeTaskRepository();
        var providerClientFactory = new FakeProviderClientFactory();
        providerClientFactory.ClientToReturn.AssignedTasksToReturn =
        [
            new ProviderTaskSummary("gh-2", "New task", null, "Todo", false, false, DateTimeOffset.UtcNow, null, null),
        ];
        var appSettingsStore = new FakeAppSettingsStore();
        var useCase = CreateUseCase(taskRepository, providerClientFactory, appSettingsStore);

        IReadOnlyList<TaskDto> result = await useCase.ExecuteAsync("github");

        Assert.Single(result);
        Assert.Single(taskRepository.Tasks);
    }
}
