using System.Collections.ObjectModel;
using TaskEngine.Application.Abstractions;
using TaskEngine.Application.Providers;
using TaskEngine.Application.Tasks;
using TaskEngine.Desktop.Mvvm;
using TaskEngine.Domain.Entities;

namespace TaskEngine.Desktop.ViewModels;

/// <summary>
/// One selectable "item de atividade" (Schema-003, ERS-Tarefas.md/ERS-Monitoramento.md) row in
/// <see cref="ConcludeTaskModalViewModel"/>'s checklist - a file (grouped by folder, see
/// <see cref="ConcludeFolderGroup"/>) or a browser page. Only <see cref="Id"/> matters for the
/// eventual <see cref="ConcludeTaskRequest.SelectedActivityIds"/> - the rest is display only.
/// </summary>
public sealed class ConcludeActivityItem : ObservableObject
{
    private readonly Action _onSelectionChanged;
    private bool _isSelected;

    public ConcludeActivityItem(
        Guid id, string label, string? url, TimeSpan duration, bool isSelected, Action onSelectionChanged)
    {
        Id = id;
        Label = label;
        Url = url;
        DurationLabel = ConcludeTaskModalViewModel.FormatDuration(duration);
        _isSelected = isSelected;
        _onSelectionChanged = onSelectionChanged;
    }

    public Guid Id { get; }

    public string Label { get; }

    /// <summary>Non-null only for browser items (<see cref="ActivityItemType.Browser"/>).</summary>
    public string? Url { get; }

    public string DurationLabel { get; }

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (SetProperty(ref _isSelected, value))
            {
                _onSelectionChanged();
            }
        }
    }
}

/// <summary>
/// Files sharing a folder, the same grouping convention as the prototype's own groupByFolder
/// (docs/prototipo/TaskEngine Dashboard.dc.html) - root-level files land in a synthetic "/" group.
/// <see cref="AllSelected"/> is a live-computed "select all in this folder" checkbox.
/// </summary>
public sealed class ConcludeFolderGroup : ObservableObject
{
    public ConcludeFolderGroup(string folderPath, IReadOnlyList<ConcludeActivityItem> items)
    {
        FolderPath = folderPath;
        Items = items;
    }

    public string FolderPath { get; }

    public IReadOnlyList<ConcludeActivityItem> Items { get; }

    /// <summary>True when every item in this folder is selected; setting it toggles every item at once.</summary>
    public bool AllSelected
    {
        get => Items.Count > 0 && Items.All(i => i.IsSelected);
        set
        {
            foreach (ConcludeActivityItem item in Items)
            {
                item.IsSelected = value;
            }
        }
    }

    /// <summary>Called by the owning view model after any item's selection changes, so this folder's header checkbox reflects the new "all selected" state.</summary>
    public void NotifySelectionChanged() => OnPropertyChanged(nameof(AllSelected));
}

/// <summary>
/// View model for the "Concluir tarefa" modal (RF-007/Schema-003, ERS-Tarefas.md; issue #18): lets
/// the user search and check which recorded activity items (files/browser pages) from the task's
/// own active periods actually belong to it, then confirms via <see cref="ConcludeTaskUseCase"/>.
///
/// Owns its own open/closed visual state (<see cref="IsOpen"/>) and calls the use case itself
/// (same convention already used throughout this codebase - e.g. <c>DashboardViewModel</c> calls
/// <c>StartWorkSessionUseCase</c> directly, no separate service layer). The hosting screen's view
/// model (Dashboard/Tarefas/DetalhesTarefa) only decides *when* to open it (in reaction to
/// "Concluir" on <c>StatusButtonView</c>, mirroring how that component already delegates the
/// InProgress-&gt;Done decision entirely to the host) and reacts to <see cref="Concluded"/> to
/// reload its own list/detail data.
///
/// GAP flagged, not invented: the checklist is built from <see cref="IMonitoredActivityRepository"/>
/// (the task-independent activity stream the monitoring module records - see that interface's own
/// doc comment), but <see cref="ConcludeTaskUseCase"/> only ever marks
/// <see cref="ActivityInterval.SelectedAtConclusion"/> on intervals already attributed to one of the
/// task's own <c>WorkSession.Activities</c> (via <c>WorkSession.RecordActivity</c>). Nothing in this
/// codebase yet bridges the two (that would be RF-004's "retroactive association" - no use case for
/// it exists yet). Selecting a monitored item here is therefore safe (never throws - unmatched ids
/// are simply ignored by <c>WorkSession.ApplyActivitySelection</c>) but will only actually affect
/// the computed human/AI duration once that bridge exists.
/// </summary>
public sealed class ConcludeTaskModalViewModel : ObservableObject
{
    private readonly IWorkSessionRepository _workSessionRepository;
    private readonly IMonitoredActivityRepository _monitoredActivityRepository;
    private readonly ConcludeTaskUseCase _concludeTaskUseCase;

    private Guid _taskId;
    private List<ConcludeActivityItem> _allFileItems = [];
    private List<ConcludeActivityItem> _allBrowserItems = [];

    private bool _isOpen;
    private bool _isBusy;
    private string _searchText = string.Empty;
    private string _selectedCountLabel = BuildCountLabel(0, 0);
    private string? _errorMessage;

    public ConcludeTaskModalViewModel(
        IWorkSessionRepository workSessionRepository,
        IMonitoredActivityRepository monitoredActivityRepository,
        ConcludeTaskUseCase concludeTaskUseCase)
    {
        _workSessionRepository = workSessionRepository;
        _monitoredActivityRepository = monitoredActivityRepository;
        _concludeTaskUseCase = concludeTaskUseCase;

        ConfirmCommand = new AsyncRelayCommand(_ => ConfirmAsync(CancellationToken.None));
        CancelCommand = new RelayCommand(_ => Close());
    }

    public ObservableCollection<ConcludeFolderGroup> FolderGroups { get; } = [];

    public ObservableCollection<ConcludeActivityItem> BrowserItems { get; } = [];

    public bool IsOpen
    {
        get => _isOpen;
        private set => SetProperty(ref _isOpen, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set => SetProperty(ref _isBusy, value);
    }

    /// <summary>Two-way bound search box - filters both lists client-side by label/url, same convention as <c>TarefasViewModel.SearchText</c>.</summary>
    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetProperty(ref _searchText, value))
            {
                RebuildVisibleGroups();
            }
        }
    }

    public string SelectedCountLabel
    {
        get => _selectedCountLabel;
        private set => SetProperty(ref _selectedCountLabel, value);
    }

    public string? ErrorMessage
    {
        get => _errorMessage;
        private set
        {
            if (SetProperty(ref _errorMessage, value))
            {
                OnPropertyChanged(nameof(HasError));
            }
        }
    }

    public bool HasError => ErrorMessage is not null;

    /// <summary>True once loading finished and there is no monitored activity at all for the task's active periods - shown instead of an empty list.</summary>
    public bool HasNoItems => !IsBusy && _allFileItems.Count == 0 && _allBrowserItems.Count == 0;

    public AsyncRelayCommand ConfirmCommand { get; }

    public RelayCommand CancelCommand { get; }

    /// <summary>Raised after a successful conclusion, with the task id, so the hosting screen can reload its own data - see class-level doc comment.</summary>
    public event Action<Guid>? Concluded;

    /// <summary>
    /// Opens the modal for <paramref name="taskId"/>: loads every monitored activity item (RF-007)
    /// overlapping any of the task's *active* work-session periods. RN-012 excludes pauses - each
    /// Active session's own [StartedAt, EndedAt ?? now] window is queried separately rather than one
    /// overall min..max span, so a gap covered only by a pause period is never included. Every item
    /// starts pre-selected, matching docs/prototipo/TaskEngine Dashboard.dc.html's own default.
    /// </summary>
    public async Task OpenAsync(Guid taskId, CancellationToken cancellationToken = default)
    {
        _taskId = taskId;
        ErrorMessage = null;
        _searchText = string.Empty;
        OnPropertyChanged(nameof(SearchText));
        IsBusy = true;
        IsOpen = true;

        IReadOnlyList<WorkSession> sessions = await _workSessionRepository.ListByTaskIdAsync(taskId, cancellationToken);

        var byId = new Dictionary<Guid, ActivityInterval>();
        foreach (WorkSession session in sessions.Where(s => s.Type == WorkSessionType.Active))
        {
            DateTimeOffset from = session.StartedAt;
            DateTimeOffset to = session.EndedAt ?? DateTimeOffset.UtcNow;
            if (to <= from)
            {
                continue;
            }

            IReadOnlyList<ActivityInterval> activities =
                await _monitoredActivityRepository.ListByPeriodAsync(from, to, cancellationToken);
            foreach (ActivityInterval activity in activities)
            {
                byId[activity.Id] = activity;
            }
        }

        _allFileItems = byId.Values
            .Where(a => a.Type != ActivityItemType.Browser)
            .OrderBy(a => a.Path, StringComparer.OrdinalIgnoreCase)
            .Select(a => new ConcludeActivityItem(
                a.Id, a.Path ?? "(sem caminho)", url: null, a.Duration, isSelected: true, RecalculateSelection))
            .ToList();

        _allBrowserItems = byId.Values
            .Where(a => a.Type == ActivityItemType.Browser)
            .OrderBy(a => a.Path, StringComparer.OrdinalIgnoreCase)
            .Select(a => new ConcludeActivityItem(
                a.Id, a.Path ?? "(endereço desconhecido)", a.Path, a.Duration, isSelected: true, RecalculateSelection))
            .ToList();

        IsBusy = false;
        RebuildVisibleGroups();
    }

    public void Close() => IsOpen = false;

    private async Task ConfirmAsync(CancellationToken cancellationToken)
    {
        ErrorMessage = null;

        var selectedIds = _allFileItems.Concat(_allBrowserItems)
            .Where(i => i.IsSelected)
            .Select(i => i.Id)
            .ToHashSet();

        try
        {
            await _concludeTaskUseCase.ExecuteAsync(new ConcludeTaskRequest(_taskId, selectedIds), cancellationToken);
            Close();
            Concluded?.Invoke(_taskId);
        }
        catch (ProviderFrozenException ex)
        {
            // RF-013/CA-013.1: conclusion is blocked outright while the provider is frozen - the
            // task is left exactly as it was; surfaced here instead of crashing the app.
            ErrorMessage = ex.Message;
        }
        catch (InvalidOperationException ex)
        {
            // e.g. task no longer found, or no longer in a completable status (RN-006) - surfaced
            // the same way rather than letting it bubble up and crash the app.
            ErrorMessage = ex.Message;
        }
    }

    /// <summary>Called by every <see cref="ConcludeActivityItem"/> this view model owns whenever its selection changes.</summary>
    private void RecalculateSelection()
    {
        foreach (ConcludeFolderGroup group in FolderGroups)
        {
            group.NotifySelectionChanged();
        }

        var total = _allFileItems.Count + _allBrowserItems.Count;
        var selected = _allFileItems.Count(i => i.IsSelected) + _allBrowserItems.Count(i => i.IsSelected);
        SelectedCountLabel = BuildCountLabel(selected, total);
    }

    private void RebuildVisibleGroups()
    {
        var query = SearchText.Trim();

        IEnumerable<ConcludeActivityItem> MatchingFiles() => string.IsNullOrEmpty(query)
            ? _allFileItems
            : _allFileItems.Where(i => i.Label.Contains(query, StringComparison.OrdinalIgnoreCase));

        IEnumerable<ConcludeActivityItem> MatchingBrowsers() => string.IsNullOrEmpty(query)
            ? _allBrowserItems
            : _allBrowserItems.Where(i =>
                i.Label.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                (i.Url?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false));

        FolderGroups.Clear();
        foreach (ConcludeFolderGroup group in GroupByFolder(MatchingFiles()))
        {
            FolderGroups.Add(group);
        }

        BrowserItems.Clear();
        foreach (ConcludeActivityItem item in MatchingBrowsers())
        {
            BrowserItems.Add(item);
        }

        RecalculateSelection();
        OnPropertyChanged(nameof(HasNoItems));
    }

    /// <summary>Groups files by their containing folder path, sorted alphabetically - same convention as the prototype's own groupByFolder (root-level files land in a synthetic "/" group).</summary>
    public static IReadOnlyList<ConcludeFolderGroup> GroupByFolder(IEnumerable<ConcludeActivityItem> items)
    {
        return items
            .GroupBy(GetFolderPath, StringComparer.OrdinalIgnoreCase)
            .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase)
            .Select(g => new ConcludeFolderGroup(g.Key, g.ToList()))
            .ToList();
    }

    public static string GetFolderPath(ConcludeActivityItem item)
    {
        var path = item.Label;
        var lastSeparator = path.LastIndexOf('/');
        return lastSeparator >= 0 ? path[..lastSeparator] : "/";
    }

    public static string BuildCountLabel(int selected, int total) => $"{selected} de {total} itens selecionados";

    public static string FormatDuration(TimeSpan duration)
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
