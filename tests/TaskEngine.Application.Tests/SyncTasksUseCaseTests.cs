using TaskEngine.Application.Providers;
using TaskEngine.Application.Tasks;
using TaskEngine.Application.WorkSessions;
using TaskEngine.Domain.Entities;
using TaskStatus = TaskEngine.Domain.Entities.TaskStatus;

namespace TaskEngine.Application.Tests;

public class SyncTasksUseCaseTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 17, 12, 0, 0, TimeSpan.Zero);

    private static SyncTasksUseCase CreateUseCase(
        FakeTaskRepository taskRepository,
        FakeProviderClientFactory providerClientFactory,
        FakeAppSettingsStore appSettingsStore,
        FakeWorkSessionRepository? workSessionRepository = null)
    {
        workSessionRepository ??= new FakeWorkSessionRepository();
        return new SyncTasksUseCase(
            taskRepository,
            providerClientFactory,
            appSettingsStore,
            new PauseWorkSessionUseCase(taskRepository, workSessionRepository),
            new StartWorkSessionUseCase(taskRepository, workSessionRepository),
            new EndWorkSessionUseCase(taskRepository, workSessionRepository));
    }

    [Fact]
    public async Task ExecuteAsync_WhenProviderIsFrozen_ThrowsAndDoesNotCallProvider()
    {
        var taskRepository = new FakeTaskRepository();
        var providerClientFactory = new FakeProviderClientFactory();
        var appSettingsStore = new FakeAppSettingsStore();
        await appSettingsStore.SetAsync(ProviderSettingsKeys.Frozen("github"), "true", CancellationToken.None);
        var useCase = CreateUseCase(taskRepository, providerClientFactory, appSettingsStore);

        var exception = await Assert.ThrowsAsync<ProviderFrozenException>(() => useCase.ExecuteAsync("github"));

        Assert.Equal("github", exception.ProviderId);
        Assert.Null(providerClientFactory.LastRequestedProviderId);
    }

    [Fact]
    public async Task ExecuteAsync_WithNewToDoTask_CreatesItLocally()
    {
        var taskRepository = new FakeTaskRepository();
        var providerClientFactory = new FakeProviderClientFactory();
        providerClientFactory.ClientToReturn.AssignedTasksToReturn =
        [
            new ProviderTaskSummary("gh-1", "Write report", "desc", "Todo", false, false, Now, "High", "https://x/1"),
        ];
        var appSettingsStore = new FakeAppSettingsStore();
        var useCase = CreateUseCase(taskRepository, providerClientFactory, appSettingsStore);

        IReadOnlyList<TaskDto> result = await useCase.ExecuteAsync("github");

        TaskDto dto = Assert.Single(result);
        Assert.Equal("Write report", dto.Title);
        Assert.Equal(TaskStatus.ToDo.ToString(), dto.Status);
        Assert.Equal("High", dto.Priority);
        Assert.Equal("gh-1", dto.ProviderTaskId);

        TaskItem persisted = Assert.Single(taskRepository.Tasks);
        Assert.Equal("github", persisted.ProviderId);
        Assert.Equal("Todo", persisted.ProviderStatusName);
    }

    [Fact]
    public async Task ExecuteAsync_WithNewInProgressTask_CreatesAndStartsIt()
    {
        var taskRepository = new FakeTaskRepository();
        var providerClientFactory = new FakeProviderClientFactory();
        providerClientFactory.ClientToReturn.AssignedTasksToReturn =
        [
            new ProviderTaskSummary("gh-1", "Write report", null, "In Progress", true, false, Now, null, null),
        ];
        var appSettingsStore = new FakeAppSettingsStore();
        var useCase = CreateUseCase(taskRepository, providerClientFactory, appSettingsStore);

        IReadOnlyList<TaskDto> result = await useCase.ExecuteAsync("github");

        Assert.Equal(TaskStatus.InProgress.ToString(), Assert.Single(result).Status);
    }

    [Fact]
    public async Task ExecuteAsync_WithNewAlreadyDoneTaskCreatedBeforeConnection_ExcludesIt()
    {
        // RN-001: a task never seen before, already done, created before the connection boundary
        // established on this very first sync call (DateTimeOffset.UtcNow at call time) - so any
        // CreatedAt clearly in the past is "before the connection".
        var taskRepository = new FakeTaskRepository();
        var providerClientFactory = new FakeProviderClientFactory();
        providerClientFactory.ClientToReturn.AssignedTasksToReturn =
        [
            new ProviderTaskSummary("gh-1", "Old completed task", null, "Done", false, true, Now.AddDays(-30), null, null),
        ];
        var appSettingsStore = new FakeAppSettingsStore();
        var useCase = CreateUseCase(taskRepository, providerClientFactory, appSettingsStore);

        IReadOnlyList<TaskDto> result = await useCase.ExecuteAsync("github");

        Assert.Empty(result);
        Assert.Empty(taskRepository.Tasks);
    }

    [Fact]
    public async Task ExecuteAsync_WithNewDoneTaskCreatedAfterConnection_IncludesItAsDone()
    {
        // Connection boundary is established as "now" on the first sync; a task created "now" is
        // not "before the connection", so RN-001 does not exclude it even though it's already done.
        var taskRepository = new FakeTaskRepository();
        var providerClientFactory = new FakeProviderClientFactory();
        var appSettingsStore = new FakeAppSettingsStore();
        var useCase = CreateUseCase(taskRepository, providerClientFactory, appSettingsStore);

        providerClientFactory.ClientToReturn.AssignedTasksToReturn =
        [
            new ProviderTaskSummary("gh-1", "Quick task", null, "Done", false, true, DateTimeOffset.UtcNow.AddSeconds(5), null, null),
        ];

        IReadOnlyList<TaskDto> result = await useCase.ExecuteAsync("github");

        Assert.Equal(TaskStatus.Done.ToString(), Assert.Single(result).Status);
    }

    [Fact]
    public async Task ExecuteAsync_EstablishesConnectedAtOnlyOnce()
    {
        var taskRepository = new FakeTaskRepository();
        var providerClientFactory = new FakeProviderClientFactory();
        var appSettingsStore = new FakeAppSettingsStore();
        var useCase = CreateUseCase(taskRepository, providerClientFactory, appSettingsStore);

        await useCase.ExecuteAsync("github");
        var firstConnectedAt = appSettingsStore.Values["provider:github:connectedAt"];

        await useCase.ExecuteAsync("github");
        var secondConnectedAt = appSettingsStore.Values["provider:github:connectedAt"];

        Assert.Equal(firstConnectedAt, secondConnectedAt);
    }

    [Fact]
    public async Task ExecuteAsync_WithExistingTaskStillAssigned_UpdatesPriorityAndProviderStatusName()
    {
        var taskRepository = new FakeTaskRepository();
        var task = TaskItem.Create("Write report", providerTaskId: "gh-1", providerId: "github");
        await taskRepository.AddAsync(task, CancellationToken.None);
        var providerClientFactory = new FakeProviderClientFactory();
        providerClientFactory.ClientToReturn.AssignedTasksToReturn =
        [
            new ProviderTaskSummary("gh-1", "Write report", null, "Todo", false, false, Now, "Low", null),
        ];
        var appSettingsStore = new FakeAppSettingsStore();
        var useCase = CreateUseCase(taskRepository, providerClientFactory, appSettingsStore);

        await useCase.ExecuteAsync("github");

        TaskItem persisted = Assert.Single(taskRepository.Tasks);
        Assert.Equal("Low", persisted.Priority);
        Assert.Equal("Todo", persisted.ProviderStatusName);
    }

    [Fact]
    public async Task ExecuteAsync_DoesNotDuplicateExistingTaskByProviderTaskId()
    {
        var taskRepository = new FakeTaskRepository();
        var task = TaskItem.Create("Write report", providerTaskId: "gh-1", providerId: "github");
        await taskRepository.AddAsync(task, CancellationToken.None);
        var providerClientFactory = new FakeProviderClientFactory();
        providerClientFactory.ClientToReturn.AssignedTasksToReturn =
        [
            new ProviderTaskSummary("gh-1", "Write report", null, "Todo", false, false, Now, null, null),
        ];
        var appSettingsStore = new FakeAppSettingsStore();
        var useCase = CreateUseCase(taskRepository, providerClientFactory, appSettingsStore);

        await useCase.ExecuteAsync("github");

        Assert.Single(taskRepository.Tasks);
    }

    [Fact]
    public async Task ExecuteAsync_WithInProgressTaskThatBecomesBlockedOnProvider_PausesTracking()
    {
        var taskRepository = new FakeTaskRepository();
        var workSessionRepository = new FakeWorkSessionRepository();
        var task = TaskItem.Create("Write report", providerTaskId: "gh-1", providerId: "github");
        task.Start();
        await taskRepository.AddAsync(task, CancellationToken.None);
        var session = WorkSession.Start(task.Id, Now.AddHours(-1));
        await workSessionRepository.AddAsync(session, CancellationToken.None);

        var providerClientFactory = new FakeProviderClientFactory();
        providerClientFactory.ClientToReturn.AssignedTasksToReturn =
        [
            new ProviderTaskSummary("gh-1", "Write report", null, "Blocked", false, false, Now, null, null),
        ];
        var appSettingsStore = new FakeAppSettingsStore();
        var useCase = CreateUseCase(taskRepository, providerClientFactory, appSettingsStore, workSessionRepository);

        IReadOnlyList<TaskDto> result = await useCase.ExecuteAsync("github");

        Assert.Equal(TaskStatus.Paused.ToString(), Assert.Single(result).Status);
        WorkSession pauseSession = workSessionRepository.Sessions.Single(s => s.Type == WorkSessionType.Pause);
        Assert.Equal(WorkSessionOrigin.Provider, pauseSession.Origin);
    }

    [Fact]
    public async Task ExecuteAsync_WithPausedTaskThatResumesOnProvider_ResumesTracking()
    {
        var taskRepository = new FakeTaskRepository();
        var workSessionRepository = new FakeWorkSessionRepository();
        var task = TaskItem.Create("Write report", providerTaskId: "gh-1", providerId: "github");
        task.Start();
        task.Pause();
        await taskRepository.AddAsync(task, CancellationToken.None);
        var pauseSession = WorkSession.Start(task.Id, Now.AddHours(-1), WorkSessionType.Pause, WorkSessionOrigin.Provider);
        await workSessionRepository.AddAsync(pauseSession, CancellationToken.None);

        var providerClientFactory = new FakeProviderClientFactory();
        providerClientFactory.ClientToReturn.AssignedTasksToReturn =
        [
            new ProviderTaskSummary("gh-1", "Write report", null, "In Progress", true, false, Now, null, null),
        ];
        var appSettingsStore = new FakeAppSettingsStore();
        var useCase = CreateUseCase(taskRepository, providerClientFactory, appSettingsStore, workSessionRepository);

        IReadOnlyList<TaskDto> result = await useCase.ExecuteAsync("github");

        Assert.Equal(TaskStatus.InProgress.ToString(), Assert.Single(result).Status);
        Assert.False(workSessionRepository.Sessions.Single(s => s.Id == pauseSession.Id).IsOpen);
        Assert.Contains(workSessionRepository.Sessions, s => s.Type == WorkSessionType.Active && s.IsOpen);
    }

    [Fact]
    public async Task ExecuteAsync_WithInProgressTaskThatCompletesOnProvider_ClosesSessionAndCompletesTask()
    {
        var taskRepository = new FakeTaskRepository();
        var workSessionRepository = new FakeWorkSessionRepository();
        var task = TaskItem.Create("Write report", providerTaskId: "gh-1", providerId: "github");
        task.Start();
        await taskRepository.AddAsync(task, CancellationToken.None);
        var session = WorkSession.Start(task.Id, Now.AddHours(-1));
        await workSessionRepository.AddAsync(session, CancellationToken.None);

        var providerClientFactory = new FakeProviderClientFactory();
        providerClientFactory.ClientToReturn.AssignedTasksToReturn =
        [
            new ProviderTaskSummary("gh-1", "Write report", null, "Done", false, true, Now, null, null),
        ];
        var appSettingsStore = new FakeAppSettingsStore();
        var useCase = CreateUseCase(taskRepository, providerClientFactory, appSettingsStore, workSessionRepository);

        IReadOnlyList<TaskDto> result = await useCase.ExecuteAsync("github");

        Assert.Equal(TaskStatus.Done.ToString(), Assert.Single(result).Status);
        Assert.False(workSessionRepository.Sessions.Single(s => s.Id == session.Id).IsOpen);
    }

    [Fact]
    public async Task ExecuteAsync_WithLocallyCompletedTask_NeverTouchesItRegardlessOfRemoteStatus()
    {
        var taskRepository = new FakeTaskRepository();
        var workSessionRepository = new FakeWorkSessionRepository();
        var task = TaskItem.Create("Write report", providerTaskId: "gh-1", providerId: "github");
        task.Start();
        task.Complete();
        await taskRepository.AddAsync(task, CancellationToken.None);

        var providerClientFactory = new FakeProviderClientFactory();
        providerClientFactory.ClientToReturn.AssignedTasksToReturn =
        [
            new ProviderTaskSummary("gh-1", "Write report", null, "In Progress", true, false, Now, null, null),
        ];
        var appSettingsStore = new FakeAppSettingsStore();
        var useCase = CreateUseCase(taskRepository, providerClientFactory, appSettingsStore, workSessionRepository);

        IReadOnlyList<TaskDto> result = await useCase.ExecuteAsync("github");

        Assert.Equal(TaskStatus.Done.ToString(), Assert.Single(result).Status);
        Assert.Empty(workSessionRepository.Sessions);
    }
}
