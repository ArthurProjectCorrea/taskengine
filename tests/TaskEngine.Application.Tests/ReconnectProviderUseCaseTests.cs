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
        FakeAppSettingsStore appSettingsStore)
    {
        var workSessionRepository = new FakeWorkSessionRepository();
        var syncTasksUseCase = new SyncTasksUseCase(
            taskRepository,
            providerClientFactory,
            appSettingsStore,
            new PauseWorkSessionUseCase(taskRepository, workSessionRepository),
            new StartWorkSessionUseCase(taskRepository, workSessionRepository),
            new EndWorkSessionUseCase(taskRepository, workSessionRepository));

        return new ReconnectProviderUseCase(appSettingsStore, taskRepository, providerClientFactory, syncTasksUseCase);
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
