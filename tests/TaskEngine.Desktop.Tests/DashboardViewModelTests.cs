using System.Text.RegularExpressions;
using TaskEngine.Application.Reports;
using TaskEngine.Application.Tasks;
using TaskEngine.Application.WorkSessions;
using TaskEngine.Desktop.ViewModels;
using TaskEngine.Domain.Entities;

using DomainTaskStatus = TaskEngine.Domain.Entities.TaskStatus;

namespace TaskEngine.Desktop.Tests;

public class DashboardViewModelTests
{
    private static DashboardViewModel CreateViewModel(
        FakeTaskRepository taskRepository,
        FakeWorkSessionRepository workSessionRepository)
    {
        var workScheduleStore = new FakeWorkScheduleStore();
        var unmappedTimeEntryRepository = new FakeUnmappedTimeEntryRepository();
        var generateTaskReportUseCase = new GenerateTaskReportUseCase(
            taskRepository, workSessionRepository, workScheduleStore, unmappedTimeEntryRepository);
        var startWorkSessionUseCase = new StartWorkSessionUseCase(taskRepository, workSessionRepository);
        var pauseWorkSessionUseCase = new PauseWorkSessionUseCase(taskRepository, workSessionRepository);
        var monitoredActivityRepository = new FakeMonitoredActivityRepository();
        var concludeTaskUseCase = new ConcludeTaskUseCase(
            taskRepository, workSessionRepository, monitoredActivityRepository, new FakeProviderClientFactory(), new FakeAppSettingsStore());
        var concludeTaskModalViewModel = new ConcludeTaskModalViewModel(
            workSessionRepository, monitoredActivityRepository, concludeTaskUseCase);
        var addUnmappedTimeModalViewModel = new AddUnmappedTimeModalViewModel(unmappedTimeEntryRepository);

        return new DashboardViewModel(
            taskRepository,
            workSessionRepository,
            generateTaskReportUseCase,
            startWorkSessionUseCase,
            pauseWorkSessionUseCase,
            concludeTaskModalViewModel,
            addUnmappedTimeModalViewModel);
    }

    [Fact]
    public async Task LoadAsync_TotalTasksCount_CountsEveryTask_RegardlessOfStatus()
    {
        var taskRepository = new FakeTaskRepository();
        var todo = TaskItem.Create("A fazer");
        var inProgress = TaskItem.Create("Em andamento");
        inProgress.Start();
        var done = TaskItem.Create("Concluída");
        done.Start();
        done.Complete();

        await taskRepository.AddAsync(todo, CancellationToken.None);
        await taskRepository.AddAsync(inProgress, CancellationToken.None);
        await taskRepository.AddAsync(done, CancellationToken.None);

        var viewModel = CreateViewModel(taskRepository, new FakeWorkSessionRepository());

        await viewModel.LoadAsync();

        Assert.Equal(3, viewModel.TotalTasksCount);
    }

    [Fact]
    public async Task LoadAsync_SideList_OnlyIncludesToDoAndInProgress_WithInProgressFirst()
    {
        var taskRepository = new FakeTaskRepository();
        var todo = TaskItem.Create("A fazer", createdAt: DateTimeOffset.UtcNow.AddDays(-1));
        var inProgress = TaskItem.Create("Em andamento", createdAt: DateTimeOffset.UtcNow.AddDays(-2));
        inProgress.Start();
        var done = TaskItem.Create("Concluída");
        done.Start();
        done.Complete();

        await taskRepository.AddAsync(todo, CancellationToken.None);
        await taskRepository.AddAsync(inProgress, CancellationToken.None);
        await taskRepository.AddAsync(done, CancellationToken.None);

        var viewModel = CreateViewModel(taskRepository, new FakeWorkSessionRepository());

        await viewModel.LoadAsync();

        Assert.Equal(2, viewModel.Tasks.Count);
        Assert.Equal(inProgress.Id, viewModel.Tasks[0].Id);
        Assert.Equal(todo.Id, viewModel.Tasks[1].Id);
    }

    [Fact]
    public async Task LoadAsync_SelectsATask_WithOpenActiveSession_ShowsRunningElapsedTimer()
    {
        var taskRepository = new FakeTaskRepository();
        var task = TaskItem.Create("Tarefa em andamento");
        task.Start();
        await taskRepository.AddAsync(task, CancellationToken.None);

        var workSessionRepository = new FakeWorkSessionRepository();
        WorkSession session = WorkSession.Start(task.Id, DateTimeOffset.UtcNow.AddMinutes(-5));
        await workSessionRepository.AddAsync(session, CancellationToken.None);

        var viewModel = CreateViewModel(taskRepository, workSessionRepository);

        await viewModel.LoadAsync();

        Assert.True(viewModel.HasSelection);
        Assert.False(viewModel.NoSelection);
        Assert.Equal("Sessão aberta - contando agora.", viewModel.ElapsedHint);
        Assert.Matches(new Regex(@"^\d{2}:\d{2}:\d{2}$"), viewModel.ElapsedLabel);
        Assert.NotEqual("00:00:00", viewModel.ElapsedLabel);
    }

    [Fact]
    public async Task LoadAsync_SelectsAToDoTask_WithNoSession_ShowsZeroedTimer_AndNotStartedHint()
    {
        var taskRepository = new FakeTaskRepository();
        var task = TaskItem.Create("Tarefa a fazer");
        await taskRepository.AddAsync(task, CancellationToken.None);

        var viewModel = CreateViewModel(taskRepository, new FakeWorkSessionRepository());

        await viewModel.LoadAsync();

        Assert.True(viewModel.HasSelection);
        Assert.Equal("00:00:00", viewModel.ElapsedLabel);
        Assert.Equal("Nenhuma sessão iniciada ainda.", viewModel.ElapsedHint);
        Assert.Equal("0min", viewModel.HumanLabel);
    }

    [Fact]
    public async Task LoadAsync_WithNoActiveOrToDoTasks_HasNoSelection()
    {
        var taskRepository = new FakeTaskRepository();
        var done = TaskItem.Create("Concluída");
        done.Start();
        done.Complete();
        await taskRepository.AddAsync(done, CancellationToken.None);

        var viewModel = CreateViewModel(taskRepository, new FakeWorkSessionRepository());

        await viewModel.LoadAsync();

        Assert.False(viewModel.HasSelection);
        Assert.True(viewModel.NoSelection);
    }

    [Fact]
    public async Task StatusChangeCommand_ToInProgress_StartsWorkSession_AndUpdatesTaskAndDisplay()
    {
        var taskRepository = new FakeTaskRepository();
        var task = TaskItem.Create("Tarefa a fazer");
        await taskRepository.AddAsync(task, CancellationToken.None);

        var workSessionRepository = new FakeWorkSessionRepository();
        var viewModel = CreateViewModel(taskRepository, workSessionRepository);
        await viewModel.LoadAsync();

        await viewModel.StatusChangeCommand.ExecuteAsync(DomainTaskStatus.InProgress);

        Assert.Equal(DomainTaskStatus.InProgress, viewModel.SelectedStatus);
        Assert.Equal("Sessão aberta - contando agora.", viewModel.ElapsedHint);

        TaskItem? updatedTask = await taskRepository.GetByIdAsync(task.Id, CancellationToken.None);
        Assert.Equal(DomainTaskStatus.InProgress, updatedTask!.Status);
    }

    [Fact]
    public async Task StatusChangeCommand_ToPaused_PausesWorkSession_AndUpdatesTask()
    {
        // The Dashboard's side list only ever shows Status == InProgress || ToDo (per plan scope
        // for issue #19 - full status visibility/filtering is a future "Tarefas" screen concern).
        // A task that just got paused therefore drops out of that list immediately, and with it
        // the detail panel's selection - this is the intended consequence of that filter, not a
        // bug: the underlying task/session state is still correctly updated either way.
        var taskRepository = new FakeTaskRepository();
        var task = TaskItem.Create("Tarefa em andamento");
        task.Start();
        await taskRepository.AddAsync(task, CancellationToken.None);

        var workSessionRepository = new FakeWorkSessionRepository();
        WorkSession openSession = WorkSession.Start(task.Id, DateTimeOffset.UtcNow.AddMinutes(-10));
        await workSessionRepository.AddAsync(openSession, CancellationToken.None);

        var viewModel = CreateViewModel(taskRepository, workSessionRepository);
        await viewModel.LoadAsync();

        await viewModel.StatusChangeCommand.ExecuteAsync(DomainTaskStatus.Paused);

        Assert.Empty(viewModel.Tasks);
        Assert.True(viewModel.NoSelection);

        TaskItem? updatedTask = await taskRepository.GetByIdAsync(task.Id, CancellationToken.None);
        Assert.Equal(DomainTaskStatus.Paused, updatedTask!.Status);

        IReadOnlyList<WorkSession> sessions = await workSessionRepository.ListByTaskIdAsync(task.Id, CancellationToken.None);
        Assert.Contains(sessions, s => s.Type == WorkSessionType.Pause && s.IsOpen);
        Assert.Contains(sessions, s => s.Id == openSession.Id && !s.IsOpen);
    }

    [Fact]
    public async Task StatusChangeCommand_ToDone_OpensConcludeModal_WithoutCompletingTaskDirectly()
    {
        // RF-007 requires selecting which recorded activities belong to the task before
        // conclusion, via ConcludeModal (issue #18) - the Dashboard must not silently skip that
        // step by completing the task directly here.
        var taskRepository = new FakeTaskRepository();
        var task = TaskItem.Create("Tarefa em andamento");
        task.Start();
        await taskRepository.AddAsync(task, CancellationToken.None);

        var workSessionRepository = new FakeWorkSessionRepository();
        var viewModel = CreateViewModel(taskRepository, workSessionRepository);
        await viewModel.LoadAsync();

        Assert.False(viewModel.ConcludeModal.IsOpen);

        await viewModel.StatusChangeCommand.ExecuteAsync(DomainTaskStatus.Done);

        Assert.True(viewModel.ConcludeModal.IsOpen);
        Assert.Equal(DomainTaskStatus.InProgress, viewModel.SelectedStatus);

        TaskItem? updatedTask = await taskRepository.GetByIdAsync(task.Id, CancellationToken.None);
        Assert.Equal(DomainTaskStatus.InProgress, updatedTask!.Status);
    }

    [Fact]
    public async Task ConcludeModal_Concluded_ReloadsDashboard_ReflectingTheNewStatus()
    {
        // issue #18: once ConcludeModal reports a successful conclusion, the Dashboard must
        // reload so the side list/detail panel reflect the task's new (terminal) status.
        var taskRepository = new FakeTaskRepository();
        var task = TaskItem.Create("Tarefa em andamento");
        task.Start();
        await taskRepository.AddAsync(task, CancellationToken.None);

        var workSessionRepository = new FakeWorkSessionRepository();
        var viewModel = CreateViewModel(taskRepository, workSessionRepository);
        await viewModel.LoadAsync();

        await viewModel.ConcludeModal.OpenAsync(task.Id);
        await viewModel.ConcludeModal.ConfirmCommand.ExecuteAsync(null);

        TaskItem? updatedTask = await taskRepository.GetByIdAsync(task.Id, CancellationToken.None);
        Assert.Equal(DomainTaskStatus.Done, updatedTask!.Status);
        Assert.Empty(viewModel.Tasks);
    }

    [Fact]
    public async Task LoadAsync_ComputesTimeInvestedToday_MergingOverlappingHumanAndAiActivity()
    {
        // RN-007: overlapping human/AI activity in the same window counts once toward the total.
        var taskRepository = new FakeTaskRepository();
        var task = TaskItem.Create("Tarefa");
        task.Start();
        await taskRepository.AddAsync(task, CancellationToken.None);

        var workSessionRepository = new FakeWorkSessionRepository();
        DateTimeOffset now = DateTimeOffset.UtcNow;
        WorkSession session = WorkSession.Start(task.Id, now.AddMinutes(-9));
        // Human: [-9min, -4min]; AI: [-6min, -1min] -> overlap [-6min,-4min] -> merged total 8min.
        session.RecordActivity(ActivitySource.Human, now.AddMinutes(-9), now.AddMinutes(-4));
        session.RecordActivity(ActivitySource.Ai, now.AddMinutes(-6), now.AddMinutes(-1));
        await workSessionRepository.AddAsync(session, CancellationToken.None);

        var viewModel = CreateViewModel(taskRepository, workSessionRepository);

        await viewModel.LoadAsync();

        Assert.Equal("8min", viewModel.TimeInvestedTodayLabel);
    }

    [Fact]
    public async Task LoadAsync_IgnoresPausedSessionActivity_ForInvestedTimeKpis()
    {
        var taskRepository = new FakeTaskRepository();
        var task = TaskItem.Create("Tarefa");
        task.Start();
        await taskRepository.AddAsync(task, CancellationToken.None);

        var workSessionRepository = new FakeWorkSessionRepository();
        DateTimeOffset now = DateTimeOffset.UtcNow;
        WorkSession pauseSession = WorkSession.Start(task.Id, now.AddMinutes(-5), WorkSessionType.Pause);
        await workSessionRepository.AddAsync(pauseSession, CancellationToken.None);

        var viewModel = CreateViewModel(taskRepository, workSessionRepository);

        await viewModel.LoadAsync();

        Assert.Equal("0min", viewModel.TimeInvestedTodayLabel);
        Assert.Equal("0min", viewModel.TimeInvestedWeekLabel);
    }
}
