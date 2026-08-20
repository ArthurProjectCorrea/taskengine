using TaskEngine.Application.Reports;
using TaskEngine.Application.Tasks;
using TaskEngine.Application.WorkSessions;
using TaskEngine.Desktop.ViewModels;
using TaskEngine.Desktop.ViewModels.Navigation;
using TaskEngine.Domain.Entities;

using DomainTaskStatus = TaskEngine.Domain.Entities.TaskStatus;

namespace TaskEngine.Desktop.Tests;

public class DetalhesTarefaViewModelTests
{
    private static DetalhesTarefaViewModel CreateViewModel(
        FakeTaskRepository taskRepository,
        FakeWorkSessionRepository workSessionRepository,
        FakeNavigationService? navigationService = null)
    {
        var workScheduleStore = new FakeWorkScheduleStore();
        var unmappedTimeEntryRepository = new FakeUnmappedTimeEntryRepository();
        var generateTaskReportUseCase = new GenerateTaskReportUseCase(
            taskRepository, workSessionRepository, workScheduleStore, unmappedTimeEntryRepository);
        var startWorkSessionUseCase = new StartWorkSessionUseCase(taskRepository, workSessionRepository);
        var pauseWorkSessionUseCase = new PauseWorkSessionUseCase(taskRepository, workSessionRepository);
        var concludeTaskUseCase = new ConcludeTaskUseCase(
            taskRepository, workSessionRepository, new FakeProviderClientFactory(), new FakeAppSettingsStore());
        var concludeTaskModalViewModel = new ConcludeTaskModalViewModel(
            workSessionRepository, new FakeMonitoredActivityRepository(), concludeTaskUseCase);
        var addUnmappedTimeModalViewModel = new AddUnmappedTimeModalViewModel(unmappedTimeEntryRepository);

        return new DetalhesTarefaViewModel(
            taskRepository,
            workSessionRepository,
            generateTaskReportUseCase,
            startWorkSessionUseCase,
            pauseWorkSessionUseCase,
            navigationService ?? new FakeNavigationService(),
            concludeTaskModalViewModel,
            addUnmappedTimeModalViewModel);
    }

    [Fact]
    public async Task ApplyParameter_WithAGuid_LoadsTheMatchingTask()
    {
        var taskRepository = new FakeTaskRepository();
        var task = TaskItem.Create("Corrigir cálculo de tempo não mapeado");
        task.Start();
        await taskRepository.AddAsync(task, CancellationToken.None);

        var viewModel = CreateViewModel(taskRepository, new FakeWorkSessionRepository());

        viewModel.ApplyParameter(task.Id);
        await viewModel.LoadAsync();

        Assert.True(viewModel.HasTask);
        Assert.False(viewModel.NoTask);
        Assert.Equal("Corrigir cálculo de tempo não mapeado", viewModel.Title);
        Assert.Equal(DomainTaskStatus.InProgress, viewModel.Status);
    }

    [Fact]
    public void ApplyParameter_WithNonGuidParameter_IsIgnored_LeavesHasTaskFalse()
    {
        var taskRepository = new FakeTaskRepository();
        var viewModel = CreateViewModel(taskRepository, new FakeWorkSessionRepository());

        viewModel.ApplyParameter("not-a-guid");

        Assert.False(viewModel.HasTask);
        Assert.True(viewModel.NoTask);
    }

    [Fact]
    public async Task ApplyParameter_WithUnknownTaskId_LeavesHasTaskFalse()
    {
        var taskRepository = new FakeTaskRepository();
        var viewModel = CreateViewModel(taskRepository, new FakeWorkSessionRepository());

        viewModel.ApplyParameter(Guid.NewGuid());
        await viewModel.LoadAsync();

        Assert.False(viewModel.HasTask);
    }

    [Theory]
    [InlineData(DomainTaskStatus.ToDo, false)]
    [InlineData(DomainTaskStatus.InProgress, false)]
    [InlineData(DomainTaskStatus.Paused, false)]
    [InlineData(DomainTaskStatus.Done, true)]
    [InlineData(DomainTaskStatus.DonePendingSync, true)]
    public async Task ShowReportButton_OnlyTrue_ForDoneOrDonePendingSync(DomainTaskStatus status, bool expected)
    {
        var taskRepository = new FakeTaskRepository();
        TaskItem task = TaskItem.Create("Tarefa");
        switch (status)
        {
            case DomainTaskStatus.InProgress:
                task.Start();
                break;
            case DomainTaskStatus.Paused:
                task.Start();
                task.Pause();
                break;
            case DomainTaskStatus.Done:
                task.Start();
                task.Complete();
                break;
            case DomainTaskStatus.DonePendingSync:
                task.Start();
                task.CompleteOffline();
                break;
        }

        await taskRepository.AddAsync(task, CancellationToken.None);

        var viewModel = CreateViewModel(taskRepository, new FakeWorkSessionRepository());
        viewModel.ApplyParameter(task.Id);
        await viewModel.LoadAsync();

        Assert.Equal(expected, viewModel.ShowReportButton);
    }

    [Fact]
    public async Task ReportCommand_SetsUnavailableNotice_WithoutNavigatingOrThrowing()
    {
        // RF-010/RF-011's per-task report screen does not exist yet (plan item 9) - the button
        // must not navigate anywhere broken (documented no-op, same convention as "Concluir").
        var taskRepository = new FakeTaskRepository();
        var task = TaskItem.Create("Tarefa concluída");
        task.Start();
        task.Complete();
        await taskRepository.AddAsync(task, CancellationToken.None);

        var navigationService = new FakeNavigationService();
        var viewModel = CreateViewModel(taskRepository, new FakeWorkSessionRepository(), navigationService);
        viewModel.ApplyParameter(task.Id);
        await viewModel.LoadAsync();

        Assert.False(viewModel.ShowReportUnavailableNotice);

        viewModel.ReportCommand.Execute(null);

        Assert.True(viewModel.ShowReportUnavailableNotice);
        Assert.Empty(navigationService.Calls);
    }

    [Fact]
    public async Task BackCommand_NavigatesToTarefas()
    {
        var taskRepository = new FakeTaskRepository();
        var task = TaskItem.Create("Tarefa");
        await taskRepository.AddAsync(task, CancellationToken.None);

        var navigationService = new FakeNavigationService();
        var viewModel = CreateViewModel(taskRepository, new FakeWorkSessionRepository(), navigationService);
        viewModel.ApplyParameter(task.Id);
        await viewModel.LoadAsync();

        viewModel.BackCommand.Execute(null);

        (AppSection Section, object? Parameter) call = Assert.Single(navigationService.Calls);
        Assert.Equal(AppSection.Tarefas, call.Section);
        Assert.Null(call.Parameter);
    }

    [Fact]
    public async Task StatusChangeCommand_ToDone_OpensConcludeModal_WithoutCompletingTaskDirectly()
    {
        // Same convention as DashboardViewModel/TarefasViewModel: RF-007 requires selecting which
        // recorded activities belong to the task before conclusion, via ConcludeModal (issue #18) -
        // "Concluir" must not silently complete the task here.
        var taskRepository = new FakeTaskRepository();
        var task = TaskItem.Create("Tarefa em andamento");
        task.Start();
        await taskRepository.AddAsync(task, CancellationToken.None);

        var viewModel = CreateViewModel(taskRepository, new FakeWorkSessionRepository());
        viewModel.ApplyParameter(task.Id);
        await viewModel.LoadAsync();

        Assert.False(viewModel.ConcludeModal.IsOpen);

        await viewModel.StatusChangeCommand.ExecuteAsync(DomainTaskStatus.Done);

        Assert.True(viewModel.ConcludeModal.IsOpen);

        TaskItem? updatedTask = await taskRepository.GetByIdAsync(task.Id, CancellationToken.None);
        Assert.Equal(DomainTaskStatus.InProgress, updatedTask!.Status);
    }

    [Fact]
    public async Task StatusChangeCommand_ToInProgress_StartsWorkSession_AndShowsRunningTimer()
    {
        var taskRepository = new FakeTaskRepository();
        var task = TaskItem.Create("Tarefa a fazer");
        await taskRepository.AddAsync(task, CancellationToken.None);

        var workSessionRepository = new FakeWorkSessionRepository();
        var viewModel = CreateViewModel(taskRepository, workSessionRepository);
        viewModel.ApplyParameter(task.Id);
        await viewModel.LoadAsync();

        await viewModel.StatusChangeCommand.ExecuteAsync(DomainTaskStatus.InProgress);

        Assert.Equal(DomainTaskStatus.InProgress, viewModel.Status);
        Assert.Equal("Sessão aberta - contando agora.", viewModel.ElapsedHint);

        TaskItem? updatedTask = await taskRepository.GetByIdAsync(task.Id, CancellationToken.None);
        Assert.Equal(DomainTaskStatus.InProgress, updatedTask!.Status);
    }

    [Fact]
    public async Task LoadAsync_PopulatesSessions_MostRecentFirst()
    {
        var taskRepository = new FakeTaskRepository();
        var task = TaskItem.Create("Tarefa com histórico");
        task.Start();
        await taskRepository.AddAsync(task, CancellationToken.None);

        var workSessionRepository = new FakeWorkSessionRepository();
        WorkSession older = WorkSession.Start(task.Id, DateTimeOffset.UtcNow.AddHours(-3));
        older.End(DateTimeOffset.UtcNow.AddHours(-2));
        WorkSession newer = WorkSession.Start(task.Id, DateTimeOffset.UtcNow.AddMinutes(-30));
        await workSessionRepository.AddAsync(older, CancellationToken.None);
        await workSessionRepository.AddAsync(newer, CancellationToken.None);

        var viewModel = CreateViewModel(taskRepository, workSessionRepository);
        viewModel.ApplyParameter(task.Id);
        await viewModel.LoadAsync();

        Assert.Equal(2, viewModel.Sessions.Count);
        Assert.Contains("em aberto", viewModel.Sessions[0].PeriodLabel);
    }
}
