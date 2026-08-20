using System.Collections.ObjectModel;
using TaskEngine.Application.Abstractions;
using TaskEngine.Application.Reports;
using TaskEngine.Application.WorkSessions;
using TaskEngine.Desktop.Mvvm;
using TaskEngine.Domain.Entities;
using TaskEngine.Domain.TimeTracking;

using DomainTaskStatus = TaskEngine.Domain.Entities.TaskStatus;

namespace TaskEngine.Desktop.ViewModels;

/// <summary>
/// One row of the Dashboard's "Em andamento e a fazer" side list (issue #19) - a minimal
/// presentation-only projection of <see cref="TaskItem"/>, so the view never needs to bind
/// directly to a Domain entity.
/// </summary>
public sealed class DashboardTaskListItem
{
    public DashboardTaskListItem(Guid id, string title, DomainTaskStatus status, string statusLabel)
    {
        Id = id;
        Title = title;
        Status = status;
        StatusLabel = statusLabel;
    }

    public Guid Id { get; }

    public string Title { get; }

    public DomainTaskStatus Status { get; }

    public string StatusLabel { get; }
}

/// <summary>
/// View model for the Dashboard screen (issue #19, ERS-Tarefas.md/ERS-Monitoramento.md): KPI
/// cards, the "em andamento e a fazer" list and the selected task's detail panel (timer +
/// human/AI/expediente/não-mapeado proportions). Reads exclusively from data already saved
/// locally (KPIs and the detail panel are not a live provider view - RN-013's "local data only"
/// principle for reports applies here too, even though this screen is not itself one of the
/// RF-010/RF-016 reports).
///
/// Deliberately does not invent new business calculations: the human/AI/expediente/não-mapeado
/// split for the selected task reuses <see cref="GenerateTaskReportUseCase"/> (RF-016) as-is,
/// picking out the row for the selected task, instead of recomputing RN-007's overlap-merging
/// logic here - there is no dedicated per-task report use case yet (see plan notes on issue #19).
/// The "tempo investido hoje/semana" KPIs have no existing use case to reuse (no daily/weekly
/// bucketing exists anywhere in Application yet), so they are computed here directly from
/// <see cref="IWorkSessionRepository"/> data - but even that still reuses the Domain's own
/// <see cref="TimeIntervalMerger"/> (the same RN-007 primitive <see cref="GenerateTaskReportUseCase"/>
/// itself calls) rather than reimplementing overlap handling. A future ticket could promote this
/// into a proper Application-layer use case (e.g. "CalculateInvestedTimeUseCase") for full parity
/// with the reports pipeline.
/// </summary>
public sealed class DashboardViewModel : ObservableObject
{
    private readonly ITaskRepository _taskRepository;
    private readonly IWorkSessionRepository _workSessionRepository;
    private readonly GenerateTaskReportUseCase _generateTaskReportUseCase;
    private readonly StartWorkSessionUseCase _startWorkSessionUseCase;
    private readonly PauseWorkSessionUseCase _pauseWorkSessionUseCase;

    private IReadOnlyList<TaskItem> _allTasks = [];
    private WorkSession? _selectedOpenActiveSession;
    private TimeSpan _selectedHumanTotal;

    private DashboardTaskListItem? _selectedTask;
    private int _totalTasksCount;
    private string _timeInvestedTodayLabel = "0min";
    private string _timeInvestedWeekLabel = "0min";

    private bool _hasSelection;
    private string _selectedTitle = string.Empty;
    private string _selectedMetaLabel = string.Empty;
    private DomainTaskStatus _selectedStatus = DomainTaskStatus.ToDo;
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
    private bool _showConcludeUnavailableNotice;

    public DashboardViewModel(
        ITaskRepository taskRepository,
        IWorkSessionRepository workSessionRepository,
        GenerateTaskReportUseCase generateTaskReportUseCase,
        StartWorkSessionUseCase startWorkSessionUseCase,
        PauseWorkSessionUseCase pauseWorkSessionUseCase)
    {
        _taskRepository = taskRepository;
        _workSessionRepository = workSessionRepository;
        _generateTaskReportUseCase = generateTaskReportUseCase;
        _startWorkSessionUseCase = startWorkSessionUseCase;
        _pauseWorkSessionUseCase = pauseWorkSessionUseCase;

        StatusChangeCommand = new AsyncRelayCommand(
            param => ChangeStatusAsync((DomainTaskStatus)param!, CancellationToken.None));
    }

    public ObservableCollection<DashboardTaskListItem> Tasks { get; } = [];

    /// <summary>
    /// Bound two-way to the list's <c>SelectedItem</c>. Unlike <see cref="LoadAsync"/>'s internal
    /// selection restore, changing this from the UI fires the detail load without blocking the
    /// setter (same fire-and-forget-with-internal-try/catch shape as <see cref="AsyncRelayCommand"/>)
    /// - a page-level binding setter cannot itself be awaited by the binding engine.
    /// </summary>
    public DashboardTaskListItem? SelectedTask
    {
        get => _selectedTask;
        set
        {
            if (!SetProperty(ref _selectedTask, value))
            {
                return;
            }

            ShowConcludeUnavailableNotice = false;
            _ = LoadSelectedTaskDetailAsync(value?.Id, CancellationToken.None);
        }
    }

    public int TotalTasksCount
    {
        get => _totalTasksCount;
        private set => SetProperty(ref _totalTasksCount, value);
    }

    public string TimeInvestedTodayLabel
    {
        get => _timeInvestedTodayLabel;
        private set => SetProperty(ref _timeInvestedTodayLabel, value);
    }

    public string TimeInvestedWeekLabel
    {
        get => _timeInvestedWeekLabel;
        private set => SetProperty(ref _timeInvestedWeekLabel, value);
    }

    public bool HasSelection
    {
        get => _hasSelection;
        private set
        {
            if (SetProperty(ref _hasSelection, value))
            {
                OnPropertyChanged(nameof(NoSelection));
            }
        }
    }

    /// <summary>Inverse of <see cref="HasSelection"/>, exposed only so the "empty state" label in
    /// <c>DashboardPage.xaml</c> can bind directly without a bool-inverting converter.</summary>
    public bool NoSelection => !HasSelection;

    public string SelectedTitle
    {
        get => _selectedTitle;
        private set => SetProperty(ref _selectedTitle, value);
    }

    public string SelectedMetaLabel
    {
        get => _selectedMetaLabel;
        private set => SetProperty(ref _selectedMetaLabel, value);
    }

    public DomainTaskStatus SelectedStatus
    {
        get => _selectedStatus;
        private set => SetProperty(ref _selectedStatus, value);
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

    /// <summary>Proportional width (0..1) for the "Humano" bar, relative to the largest of the four.</summary>
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

    /// <summary>
    /// True right after the user tries to conclude the selected task from the Dashboard - see
    /// <see cref="ChangeStatusAsync"/> for why that action is intentionally a no-op today.
    /// </summary>
    public bool ShowConcludeUnavailableNotice
    {
        get => _showConcludeUnavailableNotice;
        private set => SetProperty(ref _showConcludeUnavailableNotice, value);
    }

    /// <summary>Bound to <c>StatusButtonView.StatusChangeCommand</c> for the selected task.</summary>
    public AsyncRelayCommand StatusChangeCommand { get; }

    /// <summary>
    /// Loads/reloads every KPI, the side list and the selected task's detail panel from local
    /// data. Called once when the page appears and again after any status-changing action.
    /// </summary>
    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        _allTasks = await _taskRepository.ListAsync(cancellationToken);
        TotalTasksCount = _allTasks.Count;

        RebuildTaskList();
        await RecalculateInvestedTimeKpisAsync(cancellationToken);

        Guid? previousSelectedId = SelectedTask?.Id;
        DashboardTaskListItem? nextSelection =
            Tasks.FirstOrDefault(t => t.Id == previousSelectedId) ?? Tasks.FirstOrDefault();

        // Sets the backing field directly (not the public setter) so the detail load below runs
        // exactly once instead of racing a second fire-and-forget load triggered by the setter.
        SetProperty(ref _selectedTask, nextSelection, nameof(SelectedTask));
        ShowConcludeUnavailableNotice = false;
        await LoadSelectedTaskDetailAsync(nextSelection?.Id, cancellationToken);
    }

    /// <summary>
    /// Recomputes the live "tempo de execução" timer for the selected task, if it has an open
    /// active session. Deliberately has no timer/thread of its own - this project stays
    /// MAUI-free/testable (see <c>TaskEngine.Desktop.ViewModels.csproj</c>), so the hosting
    /// <c>DashboardPage</c> drives this via its own <c>IDispatcherTimer</c> (a MAUI type, already
    /// ticking on the UI thread) instead.
    /// </summary>
    public void Tick()
    {
        if (_selectedOpenActiveSession is not null)
        {
            UpdateElapsedDisplay();
        }
    }

    /// <summary>
    /// Handles a transition requested via <see cref="StatusChangeCommand"/>. <c>InProgress</c>
    /// covers both "iniciar" (ToDo) and "retomar" (Paused) - <see cref="StartWorkSessionUseCase"/>
    /// already tells those apart. <c>Done</c> is a deliberate no-op: RF-007 requires the user to
    /// pick which recorded activities belong to the task before conclusion, via a modal that does
    /// not exist yet on this screen - completing the task here would silently skip that required
    /// step, so the action is left unavailable (with a notice) instead of a degraded shortcut.
    /// </summary>
    private async Task ChangeStatusAsync(DomainTaskStatus targetStatus, CancellationToken cancellationToken)
    {
        if (SelectedTask is not { } selected)
        {
            return;
        }

        if (targetStatus == DomainTaskStatus.Done)
        {
            ShowConcludeUnavailableNotice = true;
            return;
        }

        ShowConcludeUnavailableNotice = false;

        if (targetStatus == DomainTaskStatus.InProgress)
        {
            await _startWorkSessionUseCase.ExecuteAsync(selected.Id, cancellationToken);
        }
        else if (targetStatus == DomainTaskStatus.Paused)
        {
            await _pauseWorkSessionUseCase.ExecuteAsync(selected.Id, WorkSessionOrigin.System, cancellationToken);
        }

        await LoadAsync(cancellationToken);
    }

    private void RebuildTaskList()
    {
        List<DashboardTaskListItem> active = _allTasks
            .Where(t => t.Status is DomainTaskStatus.InProgress or DomainTaskStatus.ToDo)
            .OrderByDescending(t => t.Status == DomainTaskStatus.InProgress)
            .ThenByDescending(t => t.CreatedAt)
            .Select(t => new DashboardTaskListItem(t.Id, t.Title, t.Status, StatusTransitionRules.GetStatusLabel(t.Status)))
            .ToList();

        Tasks.Clear();
        foreach (DashboardTaskListItem item in active)
        {
            Tasks.Add(item);
        }
    }

    private async Task RecalculateInvestedTimeKpisAsync(CancellationToken cancellationToken)
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        var todayStart = new DateTimeOffset(now.UtcDateTime.Date, TimeSpan.Zero);
        DateTimeOffset weekStart = todayStart.AddDays(-DaysSinceMonday(todayStart));

        var todayIntervals = new List<TimeInterval>();
        var weekIntervals = new List<TimeInterval>();

        foreach (TaskItem task in _allTasks)
        {
            IReadOnlyList<WorkSession> sessions = await _workSessionRepository.ListByTaskIdAsync(task.Id, cancellationToken);
            foreach (WorkSession session in sessions)
            {
                if (session.Type != WorkSessionType.Active)
                {
                    continue;
                }

                foreach (ActivityInterval activity in session.Activities)
                {
                    AddClipped(todayIntervals, activity.StartedAt, activity.EndedAt, todayStart, now);
                    AddClipped(weekIntervals, activity.StartedAt, activity.EndedAt, weekStart, now);
                }
            }
        }

        // RN-007: overlapping human/AI activity within the same window counts once toward the
        // total time invested - see TimeIntervalMerger, the same primitive GenerateTaskReportUseCase
        // uses for its own totals.
        TimeInvestedTodayLabel = FormatDuration(TimeIntervalMerger.TotalDuration(todayIntervals));
        TimeInvestedWeekLabel = FormatDuration(TimeIntervalMerger.TotalDuration(weekIntervals));
    }

    private async Task LoadSelectedTaskDetailAsync(Guid? taskId, CancellationToken cancellationToken)
    {
        if (taskId is not { } id)
        {
            ClearDetail();
            return;
        }

        TaskItem? task = _allTasks.FirstOrDefault(t => t.Id == id)
            ?? await _taskRepository.GetByIdAsync(id, cancellationToken);
        if (task is null)
        {
            ClearDetail();
            return;
        }

        HasSelection = true;
        SelectedTitle = task.Title;
        SelectedStatus = task.Status;
        SelectedMetaLabel = BuildMetaLabel(task);

        IReadOnlyList<WorkSession> sessions = await _workSessionRepository.ListByTaskIdAsync(id, cancellationToken);
        _selectedOpenActiveSession = sessions.FirstOrDefault(s => s.IsOpen && s.Type == WorkSessionType.Active);

        // Reuses GenerateTaskReportUseCase (RF-016) rather than recomputing the human/AI/
        // expediente/não-mapeado split here - see class-level doc comment.
        IReadOnlyList<TaskReportRow> rows = await _generateTaskReportUseCase.ExecuteAsync(cancellationToken: cancellationToken);
        TaskReportRow? row = rows.FirstOrDefault(r => r.TaskId == id);

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

        _selectedHumanTotal = human;
        UpdateElapsedDisplay();
    }

    private void ClearDetail()
    {
        HasSelection = false;
        _selectedOpenActiveSession = null;
        _selectedHumanTotal = TimeSpan.Zero;
        SelectedTitle = string.Empty;
        SelectedMetaLabel = string.Empty;
        SelectedStatus = DomainTaskStatus.ToDo;
        HumanLabel = AiLabel = OfficeLabel = UnmappedLabel = FormatDuration(TimeSpan.Zero);
        HumanFraction = AiFraction = OfficeFraction = UnmappedFraction = 0;
        UpdateElapsedDisplay();
    }

    private void UpdateElapsedDisplay()
    {
        if (_selectedOpenActiveSession is { } session)
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

        ElapsedLabel = FormatTimer(_selectedHumanTotal);
        ElapsedHint = !HasSelection
            ? string.Empty
            : SelectedStatus == DomainTaskStatus.Paused
                ? "Sessão pausada - o cronômetro está parado."
                : "Nenhuma sessão iniciada ainda.";
    }

    private static string BuildMetaLabel(TaskItem task) =>
        task.ProviderId is { } providerId
            ? task.ProviderTaskId is { } providerTaskId ? $"{providerId} · {providerTaskId}" : providerId
            : "Tarefa local (sem provedor vinculado)";

    private static void AddClipped(
        List<TimeInterval> bucket,
        DateTimeOffset start,
        DateTimeOffset end,
        DateTimeOffset windowStart,
        DateTimeOffset windowEnd)
    {
        DateTimeOffset clippedStart = start < windowStart ? windowStart : start;
        DateTimeOffset clippedEnd = end > windowEnd ? windowEnd : end;
        if (clippedEnd > clippedStart)
        {
            bucket.Add(new TimeInterval(clippedStart, clippedEnd));
        }
    }

    /// <summary>Days since the most recent Monday (0 for Monday itself), for an ISO-style week start.</summary>
    private static int DaysSinceMonday(DateTimeOffset date) => ((int)date.DayOfWeek + 6) % 7;

    /// <summary>
    /// Formats as total-hours:mm:ss (e.g. "27:41:08" past 24h) - computed manually rather than via
    /// <c>TimeSpan.ToString("hh\:mm\:ss")</c>, whose "hh" specifier wraps at 24h instead of showing
    /// total elapsed hours.
    /// </summary>
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
