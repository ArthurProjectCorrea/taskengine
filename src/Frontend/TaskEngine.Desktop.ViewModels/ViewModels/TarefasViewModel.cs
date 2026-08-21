using System.Collections.ObjectModel;
using System.Windows.Input;
using TaskEngine.Application.Abstractions;
using TaskEngine.Application.Reports;
using TaskEngine.Application.Tasks;
using TaskEngine.Application.WorkSessions;
using TaskEngine.Desktop.Mvvm;
using TaskEngine.Desktop.ViewModels.Formatting;
using TaskEngine.Desktop.ViewModels.Navigation;
using TaskEngine.Domain.Entities;

using DomainTaskStatus = TaskEngine.Domain.Entities.TaskStatus;

namespace TaskEngine.Desktop.ViewModels;

/// <summary>
/// One row of the Tarefas list (RF-003, ERS-Tarefas.md) - a minimal presentation-only projection
/// of <see cref="TaskItem"/>, mirroring <c>DashboardTaskListItem</c>'s rationale so the view never
/// binds directly to a Domain entity. Unlike the Dashboard's list, every status is represented
/// here (not just ToDo/InProgress), so each row carries its own <see cref="StatusChangeCommand"/>/
/// <see cref="ActionCommand"/> bound to this specific task, instead of a single view-model-level
/// command tracking one "selected" task.
/// </summary>
public sealed class TarefaListItem
{
    public TarefaListItem(
        Guid id,
        string title,
        string providerLabel,
        string priorityLabel,
        DomainTaskStatus status,
        string investedTimeLabel,
        string actionLabel,
        bool actionEnabled,
        ICommand statusChangeCommand,
        ICommand actionCommand,
        ICommand openDetailsCommand)
    {
        Id = id;
        Title = title;
        ProviderLabel = providerLabel;
        PriorityLabel = priorityLabel;
        Status = status;
        InvestedTimeLabel = investedTimeLabel;
        ActionLabel = actionLabel;
        ActionEnabled = actionEnabled;
        StatusChangeCommand = statusChangeCommand;
        ActionCommand = actionCommand;
        OpenDetailsCommand = openDetailsCommand;
    }

    public Guid Id { get; }

    public string Title { get; }

    public string ProviderLabel { get; }

    public string PriorityLabel { get; }

    public DomainTaskStatus Status { get; }

    /// <summary>Total time invested (RN-007 merged human+AI, via <see cref="GenerateTaskReportUseCase"/>), already formatted.</summary>
    public string InvestedTimeLabel { get; }

    /// <summary>Label for the row's single quick-action button (e.g. "Iniciar"/"Pausar"/"Retomar").</summary>
    public string ActionLabel { get; }

    public bool ActionEnabled { get; }

    /// <summary>Bound to this row's <c>StatusButtonView.StatusChangeCommand</c> - the dropdown's full set of transitions.</summary>
    public ICommand StatusChangeCommand { get; }

    /// <summary>Bound to the row's single "ação" column button - a one-click shortcut for this status's primary next step.</summary>
    public ICommand ActionCommand { get; }

    /// <summary>Bound to the row's <c>TapGestureRecognizer</c> - see <c>TarefasViewModel.OpenTaskDetails</c> for why this is a deliberate no-op today.</summary>
    public ICommand OpenDetailsCommand { get; }
}

/// <summary>
/// View model for the Tarefas (list) screen (ERS-Tarefas.md RF-001/RF-003): text search over
/// already-loaded tasks (CA-003.1), on-demand sync via <see cref="SyncTasksUseCase"/> (RF-001,
/// CA-001.1/CA-001.2), and client-side pagination (10 per page). Every status is listed here
/// (unlike the Dashboard's ToDo/InProgress-only side list) - see <see cref="TarefaListItem"/>.
///
/// Status transitions reuse exactly the same orchestration as <c>DashboardViewModel</c>
/// (<see cref="StartWorkSessionUseCase"/>/<see cref="PauseWorkSessionUseCase"/>, and the same
/// "Concluir opens the RF-007 completion modal" convention) - see <see cref="ChangeStatusAsync"/>.
/// </summary>
public sealed class TarefasViewModel : ObservableObject
{
    private const int PageSize = 10;

    private readonly ITaskRepository _taskRepository;
    private readonly SyncTasksUseCase _syncTasksUseCase;
    private readonly GenerateTaskReportUseCase _generateTaskReportUseCase;
    private readonly StartWorkSessionUseCase _startWorkSessionUseCase;
    private readonly PauseWorkSessionUseCase _pauseWorkSessionUseCase;
    private readonly IAppSettingsStore _appSettingsStore;
    private readonly INavigationService _navigationService;

    private IReadOnlyList<TaskItem> _allTasks = [];
    private IReadOnlyDictionary<Guid, double> _investedSecondsByTaskId = new Dictionary<Guid, double>();
    private int _currentPageIndex;
    private int _totalPages = 1;

    private string _searchText = string.Empty;
    private string _syncButtonLabel = "Sincronizar";
    private string? _syncErrorMessage;
    private string _countLabel = "0 tarefas";
    private string _pageLabel = "Página 1 de 1";
    private bool _showPagination;

    public TarefasViewModel(
        ITaskRepository taskRepository,
        SyncTasksUseCase syncTasksUseCase,
        GenerateTaskReportUseCase generateTaskReportUseCase,
        StartWorkSessionUseCase startWorkSessionUseCase,
        PauseWorkSessionUseCase pauseWorkSessionUseCase,
        IAppSettingsStore appSettingsStore,
        INavigationService navigationService,
        ConcludeTaskModalViewModel concludeTaskModalViewModel)
    {
        _taskRepository = taskRepository;
        _syncTasksUseCase = syncTasksUseCase;
        _generateTaskReportUseCase = generateTaskReportUseCase;
        _startWorkSessionUseCase = startWorkSessionUseCase;
        _pauseWorkSessionUseCase = pauseWorkSessionUseCase;
        _appSettingsStore = appSettingsStore;
        _navigationService = navigationService;
        ConcludeModal = concludeTaskModalViewModel;

        ConcludeModal.Concluded += OnTaskConcluded;

        SyncCommand = new AsyncRelayCommand(_ => SyncAsync(CancellationToken.None));
        PreviousPageCommand = new RelayCommand(_ => ChangePage(-1), _ => _currentPageIndex > 0);
        NextPageCommand = new RelayCommand(_ => ChangePage(1), _ => _currentPageIndex < _totalPages - 1);
    }

    public ObservableCollection<TarefaListItem> Tasks { get; } = [];

    /// <summary>Two-way bound to the search entry (CA-003.1) - filters <see cref="_allTasks"/> by title, client-side, no new backend call.</summary>
    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetProperty(ref _searchText, value))
            {
                _currentPageIndex = 0;
                RebuildVisibleTasks();
            }
        }
    }

    public AsyncRelayCommand SyncCommand { get; }

    public string SyncButtonLabel
    {
        get => _syncButtonLabel;
        private set => SetProperty(ref _syncButtonLabel, value);
    }

    /// <summary>Non-null after a failed sync (CA-001.2) - shown without crashing, existing local data is left untouched.</summary>
    public string? SyncErrorMessage
    {
        get => _syncErrorMessage;
        private set => SetProperty(ref _syncErrorMessage, value);
    }

    public bool HasSyncError => SyncErrorMessage is not null;

    /// <summary>The "Concluir tarefa" modal (RF-007, issue #18), shared by every row - see <see cref="ConcludeTaskModalViewModel"/>'s own doc comment.</summary>
    public ConcludeTaskModalViewModel ConcludeModal { get; }

    public string CountLabel
    {
        get => _countLabel;
        private set => SetProperty(ref _countLabel, value);
    }

    public string PageLabel
    {
        get => _pageLabel;
        private set => SetProperty(ref _pageLabel, value);
    }

    /// <summary>Only shown once the filtered list has more than <see cref="PageSize"/> items.</summary>
    public bool ShowPagination
    {
        get => _showPagination;
        private set => SetProperty(ref _showPagination, value);
    }

    public RelayCommand PreviousPageCommand { get; }

    public RelayCommand NextPageCommand { get; }

    /// <summary>
    /// Loads every task from local data plus the general report's per-task invested-time totals
    /// (reused as-is, same as <c>DashboardViewModel</c>, rather than recomputing RN-007's overlap
    /// merge here), then rebuilds the filtered/paginated visible list.
    /// </summary>
    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        _allTasks = await _taskRepository.ListAsync(cancellationToken);

        IReadOnlyList<TaskReportRow> rows = await _generateTaskReportUseCase.ExecuteAsync(cancellationToken: cancellationToken);
        _investedSecondsByTaskId = rows.ToDictionary(r => r.TaskId, r => r.TotalSeconds);

        RebuildVisibleTasks();
    }

    /// <summary>
    /// Sync (RF-001/CA-001.1/CA-001.2): reads the connected provider id from the same setting key
    /// the onboarding flow writes (<see cref="OnboardingViewModel.ConnectedProviderSettingKey"/>),
    /// then delegates to <see cref="SyncTasksUseCase"/>. Any failure (provider unavailable, frozen,
    /// no provider connected yet) is caught and surfaced via <see cref="SyncErrorMessage"/> instead
    /// of throwing - local data is left as-is either way, matching CA-001.2.
    /// </summary>
    private async Task SyncAsync(CancellationToken cancellationToken)
    {
        SyncErrorMessage = null;
        SyncButtonLabel = "Sincronizando...";

        try
        {
            var providerId = await _appSettingsStore.GetAsync(
                OnboardingViewModel.ConnectedProviderSettingKey, cancellationToken);
            if (providerId is null)
            {
                SyncErrorMessage = "Nenhum provedor conectado. Conecte um provedor em Configurações antes de sincronizar.";
                return;
            }

            await _syncTasksUseCase.ExecuteAsync(providerId, cancellationToken);
            await LoadAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            SyncErrorMessage = ex.Message;
        }
        finally
        {
            SyncButtonLabel = "Sincronizar";
        }
    }

    /// <summary>
    /// Handles both <see cref="TarefaListItem.StatusChangeCommand"/> (any transition picked from a
    /// row's dropdown) and <see cref="TarefaListItem.ActionCommand"/> (the row's one-click
    /// shortcut) - both ultimately call this. Identical shape to
    /// <c>DashboardViewModel.ChangeStatusAsync</c>: <c>Done</c> opens <see cref="ConcludeModal"/>
    /// (RF-007, issue #18) instead of completing the task directly.
    /// </summary>
    private async Task ChangeStatusAsync(Guid taskId, DomainTaskStatus targetStatus, CancellationToken cancellationToken)
    {
        if (targetStatus == DomainTaskStatus.Done)
        {
            await ConcludeModal.OpenAsync(taskId, cancellationToken);
            return;
        }

        if (targetStatus == DomainTaskStatus.InProgress)
        {
            await _startWorkSessionUseCase.ExecuteAsync(taskId, cancellationToken);
        }
        else if (targetStatus == DomainTaskStatus.Paused)
        {
            await _pauseWorkSessionUseCase.ExecuteAsync(taskId, WorkSessionOrigin.System, cancellationToken);
        }

        await LoadAsync(cancellationToken);
    }

    /// <summary>Reloads once <see cref="ConcludeModal"/> reports a successful conclusion, so the list reflects the row's new status.</summary>
    private void OnTaskConcluded(Guid taskId) => _ = LoadAsync(CancellationToken.None);

    /// <summary>
    /// Navigates to the Detalhes da Tarefa screen (issue #53, ERS-Tarefas.md) with
    /// <paramref name="taskId"/> as the navigation parameter - see
    /// <see cref="Navigation.INavigationService.NavigateTo(Navigation.AppSection, object?)"/> and
    /// <c>DetalhesTarefaViewModel.ApplyParameter</c>, which receives it on the other end.
    /// </summary>
    private void OpenTaskDetails(Guid taskId) =>
        _navigationService.NavigateTo(AppSection.DetalhesTarefa, taskId);

    private void ChangePage(int delta)
    {
        _currentPageIndex += delta;
        RebuildVisibleTasks();
    }

    private void RebuildVisibleTasks()
    {
        var query = _searchText.Trim();
        List<TaskItem> filtered = string.IsNullOrEmpty(query)
            ? _allTasks.ToList()
            : _allTasks.Where(t => t.Title.Contains(query, StringComparison.OrdinalIgnoreCase)).ToList();

        _totalPages = Math.Max(1, (int)Math.Ceiling(filtered.Count / (double)PageSize));
        _currentPageIndex = Math.Clamp(_currentPageIndex, 0, _totalPages - 1);

        IEnumerable<TaskItem> pageItems = filtered.Count > PageSize
            ? filtered.Skip(_currentPageIndex * PageSize).Take(PageSize)
            : filtered;

        Tasks.Clear();
        foreach (TaskItem task in pageItems)
        {
            Tasks.Add(BuildListItem(task));
        }

        ShowPagination = filtered.Count > PageSize;
        PageLabel = $"Página {_currentPageIndex + 1} de {_totalPages}";
        CountLabel = filtered.Count == 1 ? "1 tarefa" : $"{filtered.Count} tarefas";

        PreviousPageCommand.RaiseCanExecuteChanged();
        NextPageCommand.RaiseCanExecuteChanged();
    }

    private TarefaListItem BuildListItem(TaskItem task)
    {
        var investedTimeLabel = DurationFormatter.Format(
            TimeSpan.FromSeconds(_investedSecondsByTaskId.GetValueOrDefault(task.Id)));

        (string actionLabel, bool actionEnabled, DomainTaskStatus? actionTarget) = task.Status switch
        {
            DomainTaskStatus.ToDo => ("Iniciar", true, (DomainTaskStatus?)DomainTaskStatus.InProgress),
            DomainTaskStatus.InProgress => ("Pausar", true, (DomainTaskStatus?)DomainTaskStatus.Paused),
            DomainTaskStatus.Paused => ("Retomar", true, (DomainTaskStatus?)DomainTaskStatus.InProgress),
            DomainTaskStatus.DonePendingSync => ("Sincronização pendente", false, (DomainTaskStatus?)null),
            _ => ("Concluída", false, (DomainTaskStatus?)null),
        };

        var taskId = task.Id;
        var statusChangeCommand = new AsyncRelayCommand(
            param => ChangeStatusAsync(taskId, (DomainTaskStatus)param!, CancellationToken.None));
        var actionCommand = new AsyncRelayCommand(
            _ => actionTarget is { } target ? ChangeStatusAsync(taskId, target, CancellationToken.None) : Task.CompletedTask,
            _ => actionEnabled);
        var openDetailsCommand = new RelayCommand(_ => OpenTaskDetails(taskId));

        return new TarefaListItem(
            task.Id,
            task.Title,
            task.ProviderId ?? "Tarefa local",
            task.Priority ?? "-",
            task.Status,
            investedTimeLabel,
            actionLabel,
            actionEnabled,
            statusChangeCommand,
            actionCommand,
            openDetailsCommand);
    }

}
