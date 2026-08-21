using TaskEngine.Application.Reports;
using TaskEngine.Application.Tasks;
using TaskEngine.Application.WorkSessions;
using TaskEngine.Desktop.Mvvm;
using TaskEngine.Desktop.ViewModels;
using TaskEngine.Desktop.ViewModels.Navigation;
using TaskEngine.Domain.Entities;

using DomainTaskStatus = TaskEngine.Domain.Entities.TaskStatus;

namespace TaskEngine.Desktop.Tests;

public class TarefasViewModelTests
{
    private static TarefasViewModel CreateViewModel(
        FakeTaskRepository taskRepository,
        FakeAppSettingsStore? appSettingsStore = null,
        FakeProviderClientFactory? providerClientFactory = null,
        FakeNavigationService? navigationService = null)
    {
        var workSessionRepository = new FakeWorkSessionRepository();
        var workScheduleStore = new FakeWorkScheduleStore();
        var unmappedTimeEntryRepository = new FakeUnmappedTimeEntryRepository();
        var generateTaskReportUseCase = new GenerateTaskReportUseCase(
            taskRepository, workSessionRepository, workScheduleStore, unmappedTimeEntryRepository);
        var startWorkSessionUseCase = new StartWorkSessionUseCase(taskRepository, workSessionRepository);
        var pauseWorkSessionUseCase = new PauseWorkSessionUseCase(taskRepository, workSessionRepository);
        var endWorkSessionUseCase = new EndWorkSessionUseCase(taskRepository, workSessionRepository);

        appSettingsStore ??= new FakeAppSettingsStore();
        providerClientFactory ??= new FakeProviderClientFactory();

        var syncTasksUseCase = new SyncTasksUseCase(
            taskRepository,
            providerClientFactory,
            appSettingsStore,
            startWorkSessionUseCase,
            pauseWorkSessionUseCase,
            endWorkSessionUseCase);

        var monitoredActivityRepository = new FakeMonitoredActivityRepository();
        var concludeTaskUseCase = new ConcludeTaskUseCase(
            taskRepository, workSessionRepository, monitoredActivityRepository, new FakeProviderClientFactory(), new FakeAppSettingsStore());
        var concludeTaskModalViewModel = new ConcludeTaskModalViewModel(
            workSessionRepository, monitoredActivityRepository, concludeTaskUseCase);

        return new TarefasViewModel(
            taskRepository,
            syncTasksUseCase,
            generateTaskReportUseCase,
            startWorkSessionUseCase,
            pauseWorkSessionUseCase,
            appSettingsStore,
            navigationService ?? new FakeNavigationService(),
            concludeTaskModalViewModel);
    }

    [Fact]
    public async Task LoadAsync_WithNoSearchText_ListsEveryTask()
    {
        var taskRepository = new FakeTaskRepository();
        await taskRepository.AddAsync(TaskItem.Create("Corrigir bug de sincronização"), CancellationToken.None);
        await taskRepository.AddAsync(TaskItem.Create("Revisar schema dinâmico"), CancellationToken.None);

        var viewModel = CreateViewModel(taskRepository);
        await viewModel.LoadAsync();

        Assert.Equal(2, viewModel.Tasks.Count);
        Assert.Equal("2 tarefas", viewModel.CountLabel);
    }

    [Fact]
    public async Task SearchText_FiltersByTitle_CaseInsensitive_ClientSide()
    {
        // CA-003.1: busca filtra corretamente sobre os dados já carregados.
        var taskRepository = new FakeTaskRepository();
        await taskRepository.AddAsync(TaskItem.Create("Corrigir bug de sincronização"), CancellationToken.None);
        await taskRepository.AddAsync(TaskItem.Create("Revisar schema dinâmico"), CancellationToken.None);
        await taskRepository.AddAsync(TaskItem.Create("Escrever testes de sincronização"), CancellationToken.None);

        var viewModel = CreateViewModel(taskRepository);
        await viewModel.LoadAsync();

        viewModel.SearchText = "SINCRONIZAÇÃO";

        Assert.Equal(2, viewModel.Tasks.Count);
        Assert.All(viewModel.Tasks, t => Assert.Contains("sincronização", t.Title, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task SearchText_WithNoMatches_ShowsEmptyList_WithoutCrashing()
    {
        var taskRepository = new FakeTaskRepository();
        await taskRepository.AddAsync(TaskItem.Create("Corrigir bug"), CancellationToken.None);

        var viewModel = CreateViewModel(taskRepository);
        await viewModel.LoadAsync();

        viewModel.SearchText = "não existe nenhuma tarefa assim";

        Assert.Empty(viewModel.Tasks);
        Assert.Equal("0 tarefas", viewModel.CountLabel);
    }

    [Fact]
    public async Task LoadAsync_With11Tasks_PaginatesAt10PerPage_AndShowsPagination()
    {
        var taskRepository = new FakeTaskRepository();
        for (var i = 0; i < 11; i++)
        {
            await taskRepository.AddAsync(TaskItem.Create($"Tarefa {i:D2}"), CancellationToken.None);
        }

        var viewModel = CreateViewModel(taskRepository);
        await viewModel.LoadAsync();

        Assert.True(viewModel.ShowPagination);
        Assert.Equal(10, viewModel.Tasks.Count);
        Assert.Equal("Página 1 de 2", viewModel.PageLabel);
        Assert.False(viewModel.PreviousPageCommand.CanExecute(null));
        Assert.True(viewModel.NextPageCommand.CanExecute(null));

        viewModel.NextPageCommand.Execute(null);

        Assert.Single(viewModel.Tasks);
        Assert.Equal("Página 2 de 2", viewModel.PageLabel);
        Assert.True(viewModel.PreviousPageCommand.CanExecute(null));
        Assert.False(viewModel.NextPageCommand.CanExecute(null));
    }

    [Fact]
    public async Task LoadAsync_With10OrFewerTasks_DoesNotShowPagination()
    {
        var taskRepository = new FakeTaskRepository();
        for (var i = 0; i < 10; i++)
        {
            await taskRepository.AddAsync(TaskItem.Create($"Tarefa {i:D2}"), CancellationToken.None);
        }

        var viewModel = CreateViewModel(taskRepository);
        await viewModel.LoadAsync();

        Assert.False(viewModel.ShowPagination);
        Assert.Equal(10, viewModel.Tasks.Count);
    }

    [Fact]
    public async Task ActionCommand_ForToDoTask_StartsWorkSession_AndRefreshesTheRow()
    {
        var taskRepository = new FakeTaskRepository();
        var task = TaskItem.Create("Tarefa a fazer");
        await taskRepository.AddAsync(task, CancellationToken.None);

        var viewModel = CreateViewModel(taskRepository);
        await viewModel.LoadAsync();

        TarefaListItem row = Assert.Single(viewModel.Tasks);
        Assert.Equal("Iniciar", row.ActionLabel);

        await ((AsyncRelayCommand)row.ActionCommand).ExecuteAsync(null);

        TaskItem? updatedTask = await taskRepository.GetByIdAsync(task.Id, CancellationToken.None);
        Assert.Equal(DomainTaskStatus.InProgress, updatedTask!.Status);

        TarefaListItem refreshedRow = Assert.Single(viewModel.Tasks);
        Assert.Equal(DomainTaskStatus.InProgress, refreshedRow.Status);
        Assert.Equal("Pausar", refreshedRow.ActionLabel);
    }

    [Fact]
    public async Task StatusChangeCommand_ToDone_OpensConcludeModal_WithoutCompletingTaskDirectly()
    {
        // Same convention as DashboardViewModel: RF-007 requires selecting which recorded
        // activities belong to the task before conclusion, via ConcludeModal (issue #18) -
        // "Concluir" must not silently complete the task here.
        var taskRepository = new FakeTaskRepository();
        var task = TaskItem.Create("Tarefa em andamento");
        task.Start();
        await taskRepository.AddAsync(task, CancellationToken.None);

        var viewModel = CreateViewModel(taskRepository);
        await viewModel.LoadAsync();

        Assert.False(viewModel.ConcludeModal.IsOpen);

        TarefaListItem row = Assert.Single(viewModel.Tasks);
        await ((AsyncRelayCommand)row.StatusChangeCommand).ExecuteAsync(DomainTaskStatus.Done);

        Assert.True(viewModel.ConcludeModal.IsOpen);

        TaskItem? updatedTask = await taskRepository.GetByIdAsync(task.Id, CancellationToken.None);
        Assert.Equal(DomainTaskStatus.InProgress, updatedTask!.Status);
    }

    [Fact]
    public async Task OpenDetailsCommand_NavigatesToDetalhesTarefa_WithTheRowsTaskId()
    {
        // issue #53: tapping a row navigates to Detalhes da Tarefa, carrying the task id as the
        // navigation parameter - see TarefasViewModel.OpenTaskDetails.
        var taskRepository = new FakeTaskRepository();
        var task = TaskItem.Create("Corrigir bug de sincronização");
        await taskRepository.AddAsync(task, CancellationToken.None);

        var navigationService = new FakeNavigationService();
        var viewModel = CreateViewModel(taskRepository, navigationService: navigationService);
        await viewModel.LoadAsync();

        TarefaListItem row = Assert.Single(viewModel.Tasks);
        row.OpenDetailsCommand.Execute(null);

        (AppSection Section, object? Parameter) call = Assert.Single(navigationService.Calls);
        Assert.Equal(AppSection.DetalhesTarefa, call.Section);
        Assert.Equal(task.Id, call.Parameter);
    }

    [Fact]
    public async Task SyncCommand_WithNoProviderConnected_ShowsError_WithoutCrashing()
    {
        var taskRepository = new FakeTaskRepository();
        await taskRepository.AddAsync(TaskItem.Create("Tarefa local"), CancellationToken.None);

        var viewModel = CreateViewModel(taskRepository, new FakeAppSettingsStore());
        await viewModel.LoadAsync();

        await viewModel.SyncCommand.ExecuteAsync(null);

        Assert.True(viewModel.HasSyncError);
        Assert.NotNull(viewModel.SyncErrorMessage);
        Assert.Single(viewModel.Tasks);
        Assert.Equal("Sincronizar", viewModel.SyncButtonLabel);
    }

    [Fact]
    public async Task SyncCommand_WhenProviderIsUnavailable_ShowsError_AndKeepsExistingLocalData()
    {
        // CA-001.2: provedor indisponível - mensagem de erro visível, dados locais inalterados.
        var taskRepository = new FakeTaskRepository();
        await taskRepository.AddAsync(TaskItem.Create("Tarefa já sincronizada"), CancellationToken.None);

        // Same setting key OnboardingViewModel.ConnectedProviderSettingKey writes (internal to the
        // TaskEngine.Desktop.ViewModels assembly, so not directly reachable from this test project).
        var appSettingsStore = new FakeAppSettingsStore();
        await appSettingsStore.SetAsync("provider:connected", "github", CancellationToken.None);

        var failingFactory = new FakeProviderClientFactory(new InvalidOperationException("Provedor indisponível."));

        var viewModel = CreateViewModel(taskRepository, appSettingsStore, failingFactory);
        await viewModel.LoadAsync();

        await viewModel.SyncCommand.ExecuteAsync(null);

        Assert.True(viewModel.HasSyncError);
        Assert.Single(viewModel.Tasks);
    }
}
