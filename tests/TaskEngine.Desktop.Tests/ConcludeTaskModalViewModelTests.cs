using TaskEngine.Application.Providers;
using TaskEngine.Application.Tasks;
using TaskEngine.Desktop.ViewModels;
using TaskEngine.Domain.Entities;

using DomainTaskStatus = TaskEngine.Domain.Entities.TaskStatus;

namespace TaskEngine.Desktop.Tests;

public class ConcludeTaskModalViewModelTests
{
    private static ConcludeTaskModalViewModel CreateViewModel(
        FakeTaskRepository taskRepository,
        FakeWorkSessionRepository workSessionRepository,
        FakeMonitoredActivityRepository monitoredActivityRepository,
        FakeAppSettingsStore? appSettingsStore = null)
    {
        var concludeTaskUseCase = new ConcludeTaskUseCase(
            taskRepository,
            workSessionRepository,
            monitoredActivityRepository,
            new FakeProviderClientFactory(),
            appSettingsStore ?? new FakeAppSettingsStore());

        return new ConcludeTaskModalViewModel(workSessionRepository, monitoredActivityRepository, concludeTaskUseCase);
    }

    [Fact]
    public void GroupByFolder_GroupsFilesByContainingFolder_RootFilesUseSyntheticSlash()
    {
        var a = new ConcludeActivityItem(Guid.NewGuid(), "src/services/foo.ts", null, TimeSpan.FromMinutes(5), true, () => { });
        var b = new ConcludeActivityItem(Guid.NewGuid(), "src/services/bar.ts", null, TimeSpan.FromMinutes(5), true, () => { });
        var c = new ConcludeActivityItem(Guid.NewGuid(), "README.md", null, TimeSpan.FromMinutes(5), true, () => { });

        IReadOnlyList<ConcludeFolderGroup> groups = ConcludeTaskModalViewModel.GroupByFolder([a, b, c]);

        Assert.Equal(2, groups.Count);
        Assert.Equal("/", groups[0].FolderPath);
        Assert.Equal("README.md", Assert.Single(groups[0].Items).Label);
        Assert.Equal("src/services", groups[1].FolderPath);
        Assert.Equal(2, groups[1].Items.Count);
    }

    [Fact]
    public void BuildCountLabel_FormatsSelectedOfTotal()
    {
        Assert.Equal("12 de 34 itens selecionados", ConcludeTaskModalViewModel.BuildCountLabel(12, 34));
    }

    [Fact]
    public async Task OpenAsync_OnlyIncludesActivity_OverlappingAnActiveSessionWindow_ExcludingPausePeriods()
    {
        // RF-007/RN-012: a gap covered only by a pause period must never be included in the
        // checklist - each Active session's own window is queried separately.
        var taskRepository = new FakeTaskRepository();
        var task = TaskItem.Create("Tarefa");
        task.Start();
        await taskRepository.AddAsync(task, CancellationToken.None);

        var workSessionRepository = new FakeWorkSessionRepository();
        var baseTime = DateTimeOffset.UtcNow.AddHours(-3);
        WorkSession activeBefore = WorkSession.Start(task.Id, baseTime, WorkSessionType.Active);
        activeBefore.End(baseTime.AddMinutes(30));
        WorkSession pause = WorkSession.Start(task.Id, baseTime.AddMinutes(30), WorkSessionType.Pause);
        pause.End(baseTime.AddMinutes(60));
        WorkSession activeAfter = WorkSession.Start(task.Id, baseTime.AddMinutes(60), WorkSessionType.Active);
        activeAfter.End(baseTime.AddMinutes(90));
        await workSessionRepository.AddAsync(activeBefore, CancellationToken.None);
        await workSessionRepository.AddAsync(pause, CancellationToken.None);
        await workSessionRepository.AddAsync(activeAfter, CancellationToken.None);

        var monitoredActivityRepository = new FakeMonitoredActivityRepository();
        await monitoredActivityRepository.AddAsync(
            new ActivityInterval(ActivitySource.Human, baseTime.AddMinutes(5), baseTime.AddMinutes(10), ActivityItemType.File, "a.ts"),
            CancellationToken.None);
        await monitoredActivityRepository.AddAsync(
            new ActivityInterval(ActivitySource.Human, baseTime.AddMinutes(35), baseTime.AddMinutes(40), ActivityItemType.File, "b.ts"),
            CancellationToken.None);
        await monitoredActivityRepository.AddAsync(
            new ActivityInterval(ActivitySource.Human, baseTime.AddMinutes(65), baseTime.AddMinutes(70), ActivityItemType.File, "c.ts"),
            CancellationToken.None);

        var viewModel = CreateViewModel(taskRepository, workSessionRepository, monitoredActivityRepository);

        await viewModel.OpenAsync(task.Id);

        List<string> labels = viewModel.FolderGroups.SelectMany(g => g.Items).Select(i => i.Label).ToList();
        Assert.Contains("a.ts", labels);
        Assert.Contains("c.ts", labels);
        Assert.DoesNotContain("b.ts", labels);
    }

    [Fact]
    public async Task OpenAsync_PreSelectsEveryItem_AndTogglingUpdatesSelectedCountLabel()
    {
        var taskRepository = new FakeTaskRepository();
        var task = TaskItem.Create("Tarefa");
        task.Start();
        await taskRepository.AddAsync(task, CancellationToken.None);

        var workSessionRepository = new FakeWorkSessionRepository();
        WorkSession session = WorkSession.Start(task.Id, DateTimeOffset.UtcNow.AddHours(-1), WorkSessionType.Active);
        await workSessionRepository.AddAsync(session, CancellationToken.None);

        var monitoredActivityRepository = new FakeMonitoredActivityRepository();
        DateTimeOffset now = DateTimeOffset.UtcNow;
        await monitoredActivityRepository.AddAsync(
            new ActivityInterval(ActivitySource.Human, now.AddMinutes(-30), now.AddMinutes(-25), ActivityItemType.File, "src/a.ts"),
            CancellationToken.None);
        await monitoredActivityRepository.AddAsync(
            new ActivityInterval(ActivitySource.Human, now.AddMinutes(-20), now.AddMinutes(-15), ActivityItemType.Browser, "https://example.com"),
            CancellationToken.None);

        var viewModel = CreateViewModel(taskRepository, workSessionRepository, monitoredActivityRepository);
        await viewModel.OpenAsync(task.Id);

        Assert.Equal("2 de 2 itens selecionados", viewModel.SelectedCountLabel);

        ConcludeActivityItem fileItem = viewModel.FolderGroups.Single().Items.Single();
        fileItem.IsSelected = false;

        Assert.Equal("1 de 2 itens selecionados", viewModel.SelectedCountLabel);
    }

    [Fact]
    public async Task ConfirmCommand_ConcludesTheTask_ClosesModal_AndRaisesConcluded()
    {
        var taskRepository = new FakeTaskRepository();
        var task = TaskItem.Create("Tarefa sem provedor");
        task.Start();
        await taskRepository.AddAsync(task, CancellationToken.None);

        var workSessionRepository = new FakeWorkSessionRepository();
        var viewModel = CreateViewModel(taskRepository, workSessionRepository, new FakeMonitoredActivityRepository());
        await viewModel.OpenAsync(task.Id);

        Guid? raisedTaskId = null;
        viewModel.Concluded += id => raisedTaskId = id;

        await viewModel.ConfirmCommand.ExecuteAsync(null);

        Assert.False(viewModel.IsOpen);
        Assert.Equal(task.Id, raisedTaskId);

        TaskItem? updatedTask = await taskRepository.GetByIdAsync(task.Id, CancellationToken.None);
        Assert.Equal(DomainTaskStatus.Done, updatedTask!.Status);
    }

    [Fact]
    public async Task ConfirmCommand_WhenProviderIsFrozen_ShowsErrorMessage_KeepsModalOpen_WithoutThrowing()
    {
        // RF-013/CA-013.1: conclusion is blocked outright while the provider is frozen.
        var taskRepository = new FakeTaskRepository();
        var task = TaskItem.Create("Tarefa com provedor", providerTaskId: "42", providerId: "github");
        task.Start();
        await taskRepository.AddAsync(task, CancellationToken.None);

        var appSettingsStore = new FakeAppSettingsStore();
        await appSettingsStore.SetAsync(ProviderSettingsKeys.Frozen("github"), "true", CancellationToken.None);

        var workSessionRepository = new FakeWorkSessionRepository();
        var viewModel = CreateViewModel(
            taskRepository, workSessionRepository, new FakeMonitoredActivityRepository(), appSettingsStore);
        await viewModel.OpenAsync(task.Id);

        await viewModel.ConfirmCommand.ExecuteAsync(null);

        Assert.True(viewModel.IsOpen);
        Assert.True(viewModel.HasError);
        Assert.NotNull(viewModel.ErrorMessage);

        TaskItem? updatedTask = await taskRepository.GetByIdAsync(task.Id, CancellationToken.None);
        Assert.Equal(DomainTaskStatus.InProgress, updatedTask!.Status);
    }

    [Fact]
    public async Task SearchText_FiltersChecklist_ByLabel_CaseInsensitive()
    {
        var taskRepository = new FakeTaskRepository();
        var task = TaskItem.Create("Tarefa");
        task.Start();
        await taskRepository.AddAsync(task, CancellationToken.None);

        var workSessionRepository = new FakeWorkSessionRepository();
        WorkSession session = WorkSession.Start(task.Id, DateTimeOffset.UtcNow.AddHours(-1), WorkSessionType.Active);
        await workSessionRepository.AddAsync(session, CancellationToken.None);

        var monitoredActivityRepository = new FakeMonitoredActivityRepository();
        DateTimeOffset now = DateTimeOffset.UtcNow;
        await monitoredActivityRepository.AddAsync(
            new ActivityInterval(ActivitySource.Human, now.AddMinutes(-30), now.AddMinutes(-25), ActivityItemType.File, "src/worklogCalculator.ts"),
            CancellationToken.None);
        await monitoredActivityRepository.AddAsync(
            new ActivityInterval(ActivitySource.Human, now.AddMinutes(-20), now.AddMinutes(-15), ActivityItemType.File, "tests/other.spec.ts"),
            CancellationToken.None);

        var viewModel = CreateViewModel(taskRepository, workSessionRepository, monitoredActivityRepository);
        await viewModel.OpenAsync(task.Id);

        viewModel.SearchText = "WORKLOG";

        ConcludeActivityItem item = Assert.Single(viewModel.FolderGroups.SelectMany(g => g.Items));
        Assert.Equal("src/worklogCalculator.ts", item.Label);
    }
}
