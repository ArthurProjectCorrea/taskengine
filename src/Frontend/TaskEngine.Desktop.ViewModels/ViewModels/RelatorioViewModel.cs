using System.Collections.ObjectModel;
using System.Text;
using TaskEngine.Application.Abstractions;
using TaskEngine.Application.Reports;
using TaskEngine.Desktop.Mvvm;
using TaskEngine.Desktop.ViewModels.Navigation;
using TaskEngine.Desktop.ViewModels.Reports;
using TaskEngine.Domain.Entities;

namespace TaskEngine.Desktop.ViewModels;

/// <summary>
/// One positioned block in the activity timeline (plan item 9, RF-010/CA-010.1, ERS-Tarefas.md) -
/// a single <see cref="TaskActivityTimelineRow"/> placed within its lane by
/// <see cref="TimelineLaneAllocator"/>. <see cref="LeftFraction"/>/<see cref="WidthFraction"/> are
/// 0..1 proportions of the lane's total width, meant to be bound to the view's
/// <c>AbsoluteLayout.LayoutBounds</c> with <c>XProportional,WidthProportional</c> flags - a
/// deliberately simple positioning scheme (no chart library, no third-party dependency), matching
/// the "não precisa ser um componente de gráfico sofisticado" guidance for this screen.
/// </summary>
public sealed class TimelineBarItem
{
    public TimelineBarItem(string label, string durationLabel, double leftFraction, double widthFraction)
    {
        Label = label;
        DurationLabel = durationLabel;
        LeftFraction = leftFraction;
        WidthFraction = widthFraction;
    }

    public string Label { get; }

    public string DurationLabel { get; }

    public double LeftFraction { get; }

    public double WidthFraction { get; }
}

/// <summary>One non-overlapping visual "lane" (row) of the timeline - see <see cref="TimelineLaneAllocator"/>.</summary>
public sealed class TimelineLaneItem
{
    public TimelineLaneItem(IReadOnlyList<TimelineBarItem> bars)
    {
        Bars = bars;
    }

    public IReadOnlyList<TimelineBarItem> Bars { get; }
}

/// <summary>
/// View model for the per-task Relatório screen (plan item 9, RF-010/RF-011, ERS-Tarefas.md): a
/// simplified Gantt-like timeline of the task's selected activity items
/// (<see cref="GenerateTaskActivityTimelineUseCase"/>), plus a CSV export
/// (<see cref="CsvTaskActivityTimelineWriter"/> + <see cref="IReportFileSaveDialog"/>). Reached
/// from <c>DetalhesTarefaViewModel.ReportCommand</c> via <see cref="AppSection.Relatorio"/>,
/// receiving the task id the same way <c>DetalhesTarefaViewModel</c> does - see
/// <see cref="ApplyParameter"/>.
///
/// RF-010 only applies to completed tasks - <see cref="GenerateTaskActivityTimelineUseCase"/>
/// throws <see cref="InvalidOperationException"/> if the task is missing or not
/// Done/DonePendingSync. The "Relatório" button that navigates here is already gated on
/// <c>DetalhesTarefaViewModel.ShowReportButton</c>, but both cases are still handled defensively
/// here - surfaced via <see cref="IsUnavailable"/>/<see cref="UnavailableMessage"/> instead of an
/// unhandled exception, the same "message over a broken screen" convention used elsewhere in this
/// app (e.g. the former "Relatório" no-op notice this screen replaces).
/// </summary>
public sealed class RelatorioViewModel : ObservableObject, INavigationAware
{
    private readonly ITaskRepository _taskRepository;
    private readonly GenerateTaskActivityTimelineUseCase _generateTaskActivityTimelineUseCase;
    private readonly IReportFileSaveDialog _reportFileSaveDialog;
    private readonly INavigationService _navigationService;

    private Guid _taskId;
    private IReadOnlyList<TaskActivityTimelineRow> _rows = [];

    private bool _hasData;
    private bool _hasActivities;
    private bool _isUnavailable;
    private string _unavailableMessage = string.Empty;
    private string _title = string.Empty;
    private string _metaLabel = string.Empty;

    public RelatorioViewModel(
        ITaskRepository taskRepository,
        GenerateTaskActivityTimelineUseCase generateTaskActivityTimelineUseCase,
        IReportFileSaveDialog reportFileSaveDialog,
        INavigationService navigationService)
    {
        _taskRepository = taskRepository;
        _generateTaskActivityTimelineUseCase = generateTaskActivityTimelineUseCase;
        _reportFileSaveDialog = reportFileSaveDialog;
        _navigationService = navigationService;

        BackCommand = new RelayCommand(_ => _navigationService.NavigateTo(AppSection.DetalhesTarefa, _taskId));
        ExportCsvCommand = new RelayCommand(_ => ExportCsv());
    }

    public ObservableCollection<TimelineLaneItem> Lanes { get; } = [];

    /// <summary>True once <see cref="GenerateTaskActivityTimelineUseCase"/> succeeded (even with zero rows) - gates the timeline panel/"Exportar CSV" in the view.</summary>
    public bool HasData
    {
        get => _hasData;
        private set
        {
            if (SetProperty(ref _hasData, value))
            {
                OnPropertyChanged(nameof(NoActivities));
            }
        }
    }

    /// <summary>True when <see cref="HasData"/> but there are no selected activity items to show - a distinct empty state from <see cref="IsUnavailable"/>.</summary>
    public bool HasActivities
    {
        get => _hasActivities;
        private set
        {
            if (SetProperty(ref _hasActivities, value))
            {
                OnPropertyChanged(nameof(NoActivities));
            }
        }
    }

    /// <summary>Inverse of <see cref="HasActivities"/> (only meaningful once <see cref="HasData"/> is true) - exposed so the view's empty-state label can bind directly without a bool-inverting converter, same convention as <c>DetalhesTarefaViewModel.NoTask</c>.</summary>
    public bool NoActivities => HasData && !HasActivities;

    /// <summary>True when the task is missing or not Done/DonePendingSync - see class-level doc comment.</summary>
    public bool IsUnavailable
    {
        get => _isUnavailable;
        private set => SetProperty(ref _isUnavailable, value);
    }

    public string UnavailableMessage
    {
        get => _unavailableMessage;
        private set => SetProperty(ref _unavailableMessage, value);
    }

    public string Title
    {
        get => _title;
        private set => SetProperty(ref _title, value);
    }

    /// <summary>"{Provedor} · {IdDaTarefaNoProvedor}", or a "tarefa local" label - same formatting as <c>DetalhesTarefaViewModel.MetaLabel</c>.</summary>
    public string MetaLabel
    {
        get => _metaLabel;
        private set => SetProperty(ref _metaLabel, value);
    }

    public RelayCommand BackCommand { get; }

    public RelayCommand ExportCsvCommand { get; }

    /// <summary>
    /// Receives the task id (same convention as <c>DetalhesTarefaViewModel.ApplyParameter</c>) -
    /// the only parameter <see cref="AppSection.Relatorio"/> is navigated to with. Silently ignores
    /// any other parameter shape, matching this project's "no-op over broken navigation" convention.
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

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        TaskItem? task = await _taskRepository.GetByIdAsync(_taskId, cancellationToken);
        Title = task?.Title ?? string.Empty;
        MetaLabel = BuildMetaLabel(task);

        try
        {
            _rows = await _generateTaskActivityTimelineUseCase.ExecuteAsync(_taskId, cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            _rows = [];
            Lanes.Clear();
            HasData = false;
            HasActivities = false;
            IsUnavailable = true;
            UnavailableMessage = ex.Message;
            return;
        }

        IsUnavailable = false;
        UnavailableMessage = string.Empty;
        HasData = true;
        RebuildLanes();
    }

    /// <summary>
    /// Exports <see cref="_rows"/> as CSV via <see cref="CsvTaskActivityTimelineWriter"/>, writing
    /// straight into the stream <see cref="IReportFileSaveDialog"/> hands back for the
    /// user-chosen destination. A <see langword="null"/> stream means the user cancelled the
    /// dialog - treated as a silent no-op (CA-011.1 only requires a file when the user actually
    /// confirms the export).
    /// </summary>
    private void ExportCsv()
    {
        if (!HasData)
        {
            return;
        }

        using Stream? stream = _reportFileSaveDialog.RequestSaveStream($"relatorio-tarefa-{_taskId}.csv");
        if (stream is null)
        {
            return;
        }

        using var writer = new StreamWriter(stream, Encoding.UTF8);
        CsvTaskActivityTimelineWriter.Write(_rows, writer);
    }

    /// <summary>
    /// Projects <see cref="_rows"/> into <see cref="Lanes"/> via <see cref="TimelineLaneAllocator"/>,
    /// computing each bar's <see cref="TimelineBarItem.LeftFraction"/>/<see cref="TimelineBarItem.WidthFraction"/>
    /// relative to the overall [earliest start, latest end] span (plus an 8% trailing pad so the
    /// last bar isn't flush against the view's edge - a presentation-only detail, not a business
    /// rule). Widths are floored to a small minimum so very short activities stay visible/tappable.
    /// </summary>
    private void RebuildLanes()
    {
        Lanes.Clear();
        HasActivities = _rows.Count > 0;

        if (_rows.Count == 0)
        {
            return;
        }

        DateTimeOffset overallStart = _rows.Min(r => r.StartedAt);
        DateTimeOffset overallEnd = _rows.Max(r => r.EndedAt);
        var rawSpanSeconds = Math.Max(1, (overallEnd - overallStart).TotalSeconds);
        var totalSeconds = rawSpanSeconds * 1.08;

        IReadOnlyList<IReadOnlyList<TaskActivityTimelineRow>> lanes =
            TimelineLaneAllocator.Allocate(_rows, r => r.StartedAt, r => r.EndedAt);

        foreach (IReadOnlyList<TaskActivityTimelineRow> lane in lanes)
        {
            var bars = new List<TimelineBarItem>();
            foreach (TaskActivityTimelineRow row in lane)
            {
                var left = (row.StartedAt - overallStart).TotalSeconds / totalSeconds;
                var width = Math.Max(0.015, row.Duration.TotalSeconds / totalSeconds);
                var label = string.IsNullOrEmpty(row.Path) ? "(sem caminho)" : row.Path;
                bars.Add(new TimelineBarItem(label, FormatDuration(row.Duration), left, width));
            }

            Lanes.Add(new TimelineLaneItem(bars));
        }
    }

    private static string BuildMetaLabel(TaskItem? task) =>
        task is null
            ? string.Empty
            : task.ProviderId is { } providerId
                ? task.ProviderTaskId is { } providerTaskId ? $"{providerId} · {providerTaskId}" : providerId
                : "Tarefa local (sem provedor vinculado)";

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
