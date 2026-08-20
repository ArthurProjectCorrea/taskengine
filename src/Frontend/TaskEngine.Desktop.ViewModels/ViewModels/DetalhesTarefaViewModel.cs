using System.Collections.ObjectModel;
using TaskEngine.Application.Abstractions;
using TaskEngine.Application.Reports;
using TaskEngine.Application.WorkSessions;
using TaskEngine.Desktop.Mvvm;
using TaskEngine.Desktop.ViewModels.Navigation;
using TaskEngine.Domain.Entities;

using DomainTaskStatus = TaskEngine.Domain.Entities.TaskStatus;

namespace TaskEngine.Desktop.ViewModels;

/// <summary>
/// One row of the "Registros de tempo" list (issue #53) - a minimal presentation-only projection
/// of a <see cref="WorkSession"/> (Schema-004 "Período de Acompanhamento", ERS-Tarefas.md), so the
/// view never binds directly to a Domain entity, mirroring <c>DashboardTaskListItem</c>/
/// <c>TarefaListItem</c>'s own rationale.
/// </summary>
public sealed class TimeRecordListItem
{
    public TimeRecordListItem(string periodLabel, string kindLabel, string durationLabel)
    {
        PeriodLabel = periodLabel;
        KindLabel = kindLabel;
        DurationLabel = durationLabel;
    }

    public string PeriodLabel { get; }

    public string KindLabel { get; }

    public string DurationLabel { get; }
}

/// <summary>
/// View model for the Detalhes da Tarefa screen (issue #53, ERS-Tarefas.md): the same
/// timer/human/AI/expediente/não-mapeado progress panel as the Dashboard's detail panel (see
/// <see cref="DashboardViewModel"/>'s own doc comment - reused here via
/// <see cref="GenerateTaskReportUseCase"/> the same way, not recomputed), but for a single fixed
/// task received via <see cref="ApplyParameter"/> (issue #53's <see cref="INavigationAware"/> hook)
/// instead of a selectable list. Also exposes the task's full "Registros de tempo"
/// (<see cref="WorkSession"/> history, Schema-004) and a "Relatório" action (RF-010/RF-011) that is
/// a documented no-op until the per-task activity-timeline report screen exists (plan item 9) -
/// see <see cref="RequestReport"/>.
///
/// Reached via <see cref="AppSection.DetalhesTarefa"/> - not a sidebar-level section (see that
/// enum member's own doc comment). "Voltar" (<see cref="BackCommand"/>) always returns to
/// <see cref="AppSection.Tarefas"/> - a deliberate simplification: this screen is reachable only
/// from Tarefas today, so there is no navigation history to restore.
///
/// GAP flagged, not invented: ERS-Tarefas.md Schema-001 lists "Link para o provedor" as a field of
/// a synced task, but <see cref="TaskItem"/> (TaskEngine.Domain.Entities) does not currently expose
/// any URL/link property - only <see cref="TaskItem.ProviderId"/>/<see cref="TaskItem.ProviderTaskId"/>.
/// This view model therefore cannot render a real "Abrir no provedor" link; see
/// <see cref="ProviderLinkUnavailableNote"/>.
/// </summary>
public sealed class DetalhesTarefaViewModel : ObservableObject, INavigationAware
{
    private readonly ITaskRepository _taskRepository;
    private readonly IWorkSessionRepository _workSessionRepository;
    private readonly GenerateTaskReportUseCase _generateTaskReportUseCase;
    private readonly StartWorkSessionUseCase _startWorkSessionUseCase;
    private readonly PauseWorkSessionUseCase _pauseWorkSessionUseCase;
    private readonly INavigationService _navigationService;

    private Guid _taskId;
    private WorkSession? _openActiveSession;
    private TimeSpan _humanTotal;

    private bool _hasTask;
    private string _title = string.Empty;
    private string _metaLabel = string.Empty;
    private DomainTaskStatus _status = DomainTaskStatus.ToDo;
    private string _elapsedLabel = "--:--:--";
    private string _elapsedHint = string.Empty;
    private string _humanLabel = "0min";
    private string _aiLabel = "0min";
    private string _officeLabel = "0min";
    private string _unmappedLabel = "0min";
    private double _humanFraction;
    private double _aiFraction;
    private double _officeFraction;
    private double _unmappedFraction;
    private bool _isDone;
    private bool _showReportUnavailableNotice;

    public DetalhesTarefaViewModel(
        ITaskRepository taskRepository,
        IWorkSessionRepository workSessionRepository,
        GenerateTaskReportUseCase generateTaskReportUseCase,
        StartWorkSessionUseCase startWorkSessionUseCase,
        PauseWorkSessionUseCase pauseWorkSessionUseCase,
        INavigationService navigationService,
        ConcludeTaskModalViewModel concludeTaskModalViewModel,
        AddUnmappedTimeModalViewModel addUnmappedTimeModalViewModel)
    {
        _taskRepository = taskRepository;
        _workSessionRepository = workSessionRepository;
        _generateTaskReportUseCase = generateTaskReportUseCase;
        _startWorkSessionUseCase = startWorkSessionUseCase;
        _pauseWorkSessionUseCase = pauseWorkSessionUseCase;
        _navigationService = navigationService;
        ConcludeModal = concludeTaskModalViewModel;
        AddUnmappedTimeModal = addUnmappedTimeModalViewModel;

        ConcludeModal.Concluded += OnTaskConcluded;
        AddUnmappedTimeModal.Saved += OnUnmappedTimeSaved;

        StatusChangeCommand = new AsyncRelayCommand(
            param => ChangeStatusAsync((DomainTaskStatus)param!, CancellationToken.None));
        ReportCommand = new RelayCommand(_ => RequestReport());
        BackCommand = new RelayCommand(_ => _navigationService.NavigateTo(AppSection.Tarefas));
        OpenAddUnmappedTimeCommand = new RelayCommand(_ => AddUnmappedTimeModal.Open(_taskId));
    }

    public ObservableCollection<TimeRecordListItem> Sessions { get; } = [];

    /// <summary>False until <see cref="ApplyParameter"/>'s task finishes loading (or if the task id was not found) - gates the whole detail panel in the view.</summary>
    public bool HasTask
    {
        get => _hasTask;
        private set
        {
            if (SetProperty(ref _hasTask, value))
            {
                OnPropertyChanged(nameof(NoTask));
                OnPropertyChanged(nameof(CanAddUnmappedTime));
            }
        }
    }

    /// <summary>Inverse of <see cref="HasTask"/>, exposed only so the view's "não encontrada"/loading label can bind directly without a bool-inverting converter (same convention as <c>DashboardViewModel.NoSelection</c>).</summary>
    public bool NoTask => !HasTask;

    public string Title
    {
        get => _title;
        private set => SetProperty(ref _title, value);
    }

    /// <summary>"{Provedor} · {IdDaTarefaNoProvedor}", or a "tarefa local" label - same formatting as <c>DashboardViewModel.BuildMetaLabel</c>. Also the closest available substitute for the missing provider link - see <see cref="ProviderLinkUnavailableNote"/>.</summary>
    public string MetaLabel
    {
        get => _metaLabel;
        private set => SetProperty(ref _metaLabel, value);
    }

    public DomainTaskStatus Status
    {
        get => _status;
        private set
        {
            if (SetProperty(ref _status, value))
            {
                OnPropertyChanged(nameof(CanAddUnmappedTime));
            }
        }
    }

    public string ElapsedLabel
    {
        get => _elapsedLabel;
        private set => SetProperty(ref _elapsedLabel, value);
    }

    public string ElapsedHint
    {
        get => _elapsedHint;
        private set => SetProperty(ref _elapsedHint, value);
    }

    public string HumanLabel
    {
        get => _humanLabel;
        private set => SetProperty(ref _humanLabel, value);
    }

    public string AiLabel
    {
        get => _aiLabel;
        private set => SetProperty(ref _aiLabel, value);
    }

    public string OfficeLabel
    {
        get => _officeLabel;
        private set => SetProperty(ref _officeLabel, value);
    }

    public string UnmappedLabel
    {
        get => _unmappedLabel;
        private set => SetProperty(ref _unmappedLabel, value);
    }

    /// <summary>Proportional width (0..1) for the "Humano" bar, relative to the largest of the four - same computation as <c>DashboardViewModel</c>.</summary>
    public double HumanFraction
    {
        get => _humanFraction;
        private set => SetProperty(ref _humanFraction, value);
    }

    public double AiFraction
    {
        get => _aiFraction;
        private set => SetProperty(ref _aiFraction, value);
    }

    public double OfficeFraction
    {
        get => _officeFraction;
        private set => SetProperty(ref _officeFraction, value);
    }

    public double UnmappedFraction
    {
        get => _unmappedFraction;
        private set => SetProperty(ref _unmappedFraction, value);
    }

    /// <summary>True once the task is Done/DonePendingSync - RF-010's own "para tarefas concluídas" wording; gates <see cref="ShowReportButton"/>.</summary>
    public bool IsDone
    {
        get => _isDone;
        private set
        {
            if (SetProperty(ref _isDone, value))
            {
                OnPropertyChanged(nameof(ShowReportButton));
                OnPropertyChanged(nameof(CanAddUnmappedTime));
            }
        }
    }

    /// <summary>Alias of <see cref="IsDone"/> for the view's "Relatório" button visibility binding.</summary>
    public bool ShowReportButton => IsDone;

    /// <summary>Gate for "Adicionar tempo não mapeado" (RF-006/CA-006.1: the task must be in progress or paused, i.e. already started and not yet concluded).</summary>
    public bool CanAddUnmappedTime => HasTask && !IsDone && Status != DomainTaskStatus.ToDo;

    /// <summary>The "Concluir tarefa" modal (RF-007, issue #18) for this task - see <see cref="ConcludeTaskModalViewModel"/>'s own doc comment.</summary>
    public ConcludeTaskModalViewModel ConcludeModal { get; }

    /// <summary>The "Adicionar tempo não mapeado" modal (RF-006) for this task - see <see cref="AddUnmappedTimeModalViewModel"/>'s own doc comment.</summary>
    public AddUnmappedTimeModalViewModel AddUnmappedTimeModal { get; }

    /// <summary>True right after "Relatório" was clicked - see <see cref="RequestReport"/> for why the action itself is a documented no-op today.</summary>
    public bool ShowReportUnavailableNotice
    {
        get => _showReportUnavailableNotice;
        private set => SetProperty(ref _showReportUnavailableNotice, value);
    }

    /// <summary>Explains why "Abrir no provedor" has no clickable link today - see class-level GAP doc comment.</summary>
    public string ProviderLinkUnavailableNote =>
        "TaskItem ainda não expõe um link para o provedor (Schema-001, ERS-Tarefas.md) - lacuna do backend, sinalizada e não implementada aqui.";

    /// <summary>Bound to <c>StatusButtonView.StatusChangeCommand</c> - same shape as <c>DashboardViewModel</c>/<c>TarefasViewModel</c>.</summary>
    public AsyncRelayCommand StatusChangeCommand { get; }

    public RelayCommand ReportCommand { get; }

    public RelayCommand BackCommand { get; }

    /// <summary>Opens <see cref="AddUnmappedTimeModal"/> for this task - see <see cref="CanAddUnmappedTime"/> for its visibility gate.</summary>
    public RelayCommand OpenAddUnmappedTimeCommand { get; }

    /// <summary>
    /// Receives the task id (issue #53) - the only parameter <see cref="AppSection.DetalhesTarefa"/>
    /// is navigated to with (see <c>TarefasViewModel.OpenTaskDetails</c>). Silently ignores any
    /// other parameter shape (including <see langword="null"/>) instead of throwing, matching this
    /// project's "no-op over broken navigation/UI" convention used elsewhere (e.g. the
    /// Dashboard/Tarefas "Concluir" no-op).
    /// </summary>
    public void ApplyParameter(object? parameter)
    {
        if (parameter is not Guid taskId)
        {
            return;
        }

        _taskId = taskId;
        _ = LoadAsync(CancellationToken.None);
    }

    /// <summary>Loads/reloads the task, its sessions and the progress panel from local data. Called once from <see cref="ApplyParameter"/> and again after any status-changing action.</summary>
    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        TaskItem? task = await _taskRepository.GetByIdAsync(_taskId, cancellationToken);
        if (task is null)
        {
            HasTask = false;
            return;
        }

        HasTask = true;
        Title = task.Title;
        Status = task.Status;
        IsDone = task.Status is DomainTaskStatus.Done or DomainTaskStatus.DonePendingSync;
        MetaLabel = BuildMetaLabel(task);

        IReadOnlyList<WorkSession> sessions = await _workSessionRepository.ListByTaskIdAsync(_taskId, cancellationToken);
        _openActiveSession = sessions.FirstOrDefault(s => s.IsOpen && s.Type == WorkSessionType.Active);
        RebuildSessionList(sessions);

        // Reuses GenerateTaskReportUseCase (RF-016) rather than recomputing the human/AI/
        // expediente/não-mapeado split here - same rationale as DashboardViewModel.
        IReadOnlyList<TaskReportRow> rows = await _generateTaskReportUseCase.ExecuteAsync(cancellationToken: cancellationToken);
        TaskReportRow? row = rows.FirstOrDefault(r => r.TaskId == _taskId);

        var human = TimeSpan.FromSeconds(row?.HumanSeconds ?? 0);
        var ai = TimeSpan.FromSeconds(row?.AiSeconds ?? 0);
        var office = TimeSpan.FromSeconds(row?.ScheduledSeconds ?? 0);
        var unmapped = TimeSpan.FromSeconds(row?.UnmappedSeconds ?? 0);
        var maxSeconds = new[] { human.TotalSeconds, ai.TotalSeconds, office.TotalSeconds, unmapped.TotalSeconds, 1d }.Max();

        HumanLabel = FormatDuration(human);
        AiLabel = FormatDuration(ai);
        OfficeLabel = FormatDuration(office);
        UnmappedLabel = FormatDuration(unmapped);
        HumanFraction = human.TotalSeconds / maxSeconds;
        AiFraction = ai.TotalSeconds / maxSeconds;
        OfficeFraction = office.TotalSeconds / maxSeconds;
        UnmappedFraction = unmapped.TotalSeconds / maxSeconds;

        _humanTotal = human;
        UpdateElapsedDisplay();
    }

    /// <summary>
    /// Recomputes the live "tempo de execução" timer, if the task has an open active session.
    /// Deliberately has no timer/thread of its own - same rationale as <c>DashboardViewModel.Tick</c>
    /// (this project stays MAUI-free/testable); the hosting <c>DetalhesTarefaPage</c> drives this
    /// via its own <c>IDispatcherTimer</c>.
    /// </summary>
    public void Tick()
    {
        if (_openActiveSession is not null)
        {
            UpdateElapsedDisplay();
        }
    }

    /// <summary>
    /// Handles a transition requested via <see cref="StatusChangeCommand"/> - same shape as
    /// <c>DashboardViewModel.ChangeStatusAsync</c>/<c>TarefasViewModel.ChangeStatusAsync</c>.
    /// <c>Done</c> opens <see cref="ConcludeModal"/> (RF-007, issue #18) instead of completing the
    /// task directly - <see cref="OnTaskConcluded"/> reloads once it reports success.
    /// </summary>
    private async Task ChangeStatusAsync(DomainTaskStatus targetStatus, CancellationToken cancellationToken)
    {
        if (targetStatus == DomainTaskStatus.Done)
        {
            await ConcludeModal.OpenAsync(_taskId, cancellationToken);
            return;
        }

        if (targetStatus == DomainTaskStatus.InProgress)
        {
            await _startWorkSessionUseCase.ExecuteAsync(_taskId, cancellationToken);
        }
        else if (targetStatus == DomainTaskStatus.Paused)
        {
            await _pauseWorkSessionUseCase.ExecuteAsync(_taskId, WorkSessionOrigin.System, cancellationToken);
        }

        await LoadAsync(cancellationToken);
    }

    /// <summary>Reloads once <see cref="ConcludeModal"/> reports a successful conclusion, so the panel reflects the task's new status.</summary>
    private void OnTaskConcluded(Guid taskId) => _ = LoadAsync(CancellationToken.None);

    /// <summary>Reloads once <see cref="AddUnmappedTimeModal"/> reports a successful save, so the não-mapeado total reflects the new entry.</summary>
    private void OnUnmappedTimeSaved(Guid taskId) => _ = LoadAsync(CancellationToken.None);

    /// <summary>
    /// TODO(plano geral, item 9 - relatório por tarefa): chamar
    /// <see cref="GenerateTaskActivityTimelineUseCase"/> (RF-010/RF-011) e navegar para a tela de
    /// relatório, quando ela existir. Deixado como no-op documentado por ora (mesma convenção do
    /// "Concluir" indisponível no Dashboard/Tarefas) em vez de navegar para um lugar inexistente.
    /// </summary>
    private void RequestReport()
    {
        ShowReportUnavailableNotice = true;
    }

    private void RebuildSessionList(IReadOnlyList<WorkSession> sessions)
    {
        Sessions.Clear();
        foreach (WorkSession session in sessions.OrderByDescending(s => s.StartedAt))
        {
            DateTimeOffset end = session.EndedAt ?? DateTimeOffset.UtcNow;
            var periodLabel = session.EndedAt is null
                ? $"{FormatDateTime(session.StartedAt)} - em aberto"
                : $"{FormatDateTime(session.StartedAt)} - {FormatDateTime(session.EndedAt.Value)}";
            var typeLabel = session.Type == WorkSessionType.Active ? "Ativo" : "Pausa";
            var originLabel = session.Origin == WorkSessionOrigin.System ? "Sistema" : "Provedor";

            Sessions.Add(new TimeRecordListItem(periodLabel, $"{typeLabel} · {originLabel}", FormatDuration(end - session.StartedAt)));
        }
    }

    private void UpdateElapsedDisplay()
    {
        if (_openActiveSession is { } session)
        {
            TimeSpan elapsed = DateTimeOffset.UtcNow - session.StartedAt;
            if (elapsed < TimeSpan.Zero)
            {
                elapsed = TimeSpan.Zero;
            }

            ElapsedLabel = FormatTimer(elapsed);
            ElapsedHint = "Sessão aberta - contando agora.";
            return;
        }

        ElapsedLabel = FormatTimer(_humanTotal);
        ElapsedHint = !HasTask
            ? string.Empty
            : Status == DomainTaskStatus.Paused
                ? "Sessão pausada - o cronômetro está parado."
                : IsDone
                    ? "Tempo total registrado."
                    : "Nenhuma sessão iniciada ainda.";
    }

    private static string BuildMetaLabel(TaskItem task) =>
        task.ProviderId is { } providerId
            ? task.ProviderTaskId is { } providerTaskId ? $"{providerId} · {providerTaskId}" : providerId
            : "Tarefa local (sem provedor vinculado)";

    private static string FormatDateTime(DateTimeOffset value) => value.ToLocalTime().ToString("dd/MM HH:mm");

    /// <summary>Formats as total-hours:mm:ss (e.g. "27:41:08" past 24h) - same rationale as <c>DashboardViewModel.FormatTimer</c>.</summary>
    private static string FormatTimer(TimeSpan duration)
    {
        var totalHours = (int)duration.TotalHours;
        return $"{totalHours:D2}:{duration.Minutes:D2}:{duration.Seconds:D2}";
    }

    private static string FormatDuration(TimeSpan duration)
    {
        var totalMinutes = (int)Math.Round(duration.TotalMinutes);
        var hours = totalMinutes / 60;
        var minutes = totalMinutes % 60;

        if (hours <= 0)
        {
            return $"{minutes}min";
        }

        return minutes > 0 ? $"{hours}h {minutes}min" : $"{hours}h";
    }
}
