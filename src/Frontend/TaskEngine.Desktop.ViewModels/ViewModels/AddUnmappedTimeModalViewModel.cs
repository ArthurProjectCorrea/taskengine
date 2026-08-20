using TaskEngine.Application.Abstractions;
using TaskEngine.Desktop.Mvvm;
using TaskEngine.Domain.Entities;

namespace TaskEngine.Desktop.ViewModels;

/// <summary>
/// View model for the "Adicionar tempo não mapeado" modal (RF-006/RN-004/Schema-002,
/// ERS-Tarefas.md/ERS-Monitoramento.md): a manual record of time worked on the task outside
/// TaskEngine. Same "modal owns its own open/closed state and the repository call itself, host
/// reacts to <see cref="Saved"/> to reload" shape as <see cref="ConcludeTaskModalViewModel"/> - see
/// that class's doc comment for the rationale.
/// </summary>
public sealed class AddUnmappedTimeModalViewModel : ObservableObject
{
    private readonly IUnmappedTimeEntryRepository _unmappedTimeEntryRepository;

    private Guid _taskId;
    private bool _isOpen;
    private string _hoursText = string.Empty;
    private string _minutesText = string.Empty;
    private DateTime _startDate = DateTime.Today;
    private TimeSpan _startTime = DateTime.Now.TimeOfDay;
    private string _justification = string.Empty;
    private string? _errorMessage;

    public AddUnmappedTimeModalViewModel(IUnmappedTimeEntryRepository unmappedTimeEntryRepository)
    {
        _unmappedTimeEntryRepository = unmappedTimeEntryRepository;

        SaveCommand = new AsyncRelayCommand(_ => SaveAsync(CancellationToken.None));
        CancelCommand = new RelayCommand(_ => Close());
    }

    public bool IsOpen
    {
        get => _isOpen;
        private set => SetProperty(ref _isOpen, value);
    }

    public string HoursText
    {
        get => _hoursText;
        set => SetProperty(ref _hoursText, value);
    }

    public string MinutesText
    {
        get => _minutesText;
        set => SetProperty(ref _minutesText, value);
    }

    public DateTime StartDate
    {
        get => _startDate;
        set => SetProperty(ref _startDate, value);
    }

    public TimeSpan StartTime
    {
        get => _startTime;
        set => SetProperty(ref _startTime, value);
    }

    public string Justification
    {
        get => _justification;
        set => SetProperty(ref _justification, value);
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

    public AsyncRelayCommand SaveCommand { get; }

    public RelayCommand CancelCommand { get; }

    /// <summary>Raised after a successful save, with the task id, so the hosting screen can reload its own data.</summary>
    public event Action<Guid>? Saved;

    /// <summary>Opens the modal for <paramref name="taskId"/>, resetting the form to its defaults (now, no duration/justification yet).</summary>
    public void Open(Guid taskId)
    {
        _taskId = taskId;
        ErrorMessage = null;
        HoursText = string.Empty;
        MinutesText = string.Empty;
        StartDate = DateTime.Today;
        StartTime = DateTime.Now.TimeOfDay;
        Justification = string.Empty;
        IsOpen = true;
    }

    public void Close() => IsOpen = false;

    private async Task SaveAsync(CancellationToken cancellationToken)
    {
        if (!TryParseDuration(HoursText, MinutesText, out TimeSpan duration, out var durationError))
        {
            ErrorMessage = durationError;
            return;
        }

        if (string.IsNullOrWhiteSpace(Justification))
        {
            // CA-006.2: missing justification blocks the record and must be requested back from
            // the user, matching RN-004 - not silently defaulted to some placeholder text.
            ErrorMessage = "Informe uma justificativa.";
            return;
        }

        var combined = StartDate.Date + StartTime;
        var recordedAt = new DateTimeOffset(combined, TimeZoneInfo.Local.GetUtcOffset(combined));

        try
        {
            UnmappedTimeEntry entry = UnmappedTimeEntry.Create(_taskId, duration, Justification.Trim(), recordedAt);
            await _unmappedTimeEntryRepository.AddAsync(entry, cancellationToken);
            Close();
            Saved?.Invoke(_taskId);
        }
        catch (ArgumentException ex)
        {
            // Defensive - TryParseDuration/the blank-justification check above already guard
            // UnmappedTimeEntry.Create's own invariants, but surfaced the same way rather than
            // letting it bubble up and crash the app if that ever drifts.
            ErrorMessage = ex.Message;
        }
    }

    /// <summary>Parses the hours/minutes text fields into a duration - empty defaults to 0, and the combined duration must be greater than zero, matching <see cref="UnmappedTimeEntry.Create"/>'s own guard.</summary>
    public static bool TryParseDuration(string? hoursText, string? minutesText, out TimeSpan duration, out string? error)
    {
        duration = TimeSpan.Zero;
        error = null;

        var hoursRaw = string.IsNullOrWhiteSpace(hoursText) ? "0" : hoursText.Trim();
        var minutesRaw = string.IsNullOrWhiteSpace(minutesText) ? "0" : minutesText.Trim();

        if (!int.TryParse(hoursRaw, out var hours) || hours < 0)
        {
            error = "Horas inválidas.";
            return false;
        }

        if (!int.TryParse(minutesRaw, out var minutes) || minutes < 0)
        {
            error = "Minutos inválidos.";
            return false;
        }

        duration = new TimeSpan(hours, minutes, 0);
        if (duration <= TimeSpan.Zero)
        {
            error = "Duração deve ser maior que zero.";
            return false;
        }

        return true;
    }
}
