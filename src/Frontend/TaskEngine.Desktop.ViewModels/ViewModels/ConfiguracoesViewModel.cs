using System.Collections.ObjectModel;
using System.Text;
using TaskEngine.Application.Abstractions;
using TaskEngine.Application.Providers;
using TaskEngine.Application.Reports;
using TaskEngine.Desktop.Mvvm;
using TaskEngine.Desktop.ViewModels.LocalBackup;
using TaskEngine.Desktop.ViewModels.Reports;
using TaskEngine.Domain.Entities;

namespace TaskEngine.Desktop.ViewModels;

/// <summary>
/// One toggleable weekday checkbox in the "Dias de trabalho" row (RF-002, Schema-001,
/// ERS-Configuracoes.md) - <see cref="Day"/> keeps its <see cref="System.DayOfWeek"/> identity so
/// <see cref="ConfiguracoesViewModel"/> can build a <c>WorkSchedule</c> straight from whichever
/// options are <see cref="IsSelected"/>, and <see cref="Label"/> is the single-letter Portuguese
/// initial used by the approved prototype (docs/prototipo/TaskEngine Configurações.dc.html -
/// "D S T Q Q S S", Sunday first, matching <see cref="System.DayOfWeek"/>'s own 0=Sunday order).
/// </summary>
public sealed class WorkDayOption : ObservableObject
{
    private bool _isSelected;

    public WorkDayOption(DayOfWeek day, string label, bool isSelected)
    {
        Day = day;
        Label = label;
        _isSelected = isSelected;
    }

    public DayOfWeek Day { get; }

    public string Label { get; }

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }
}

/// <summary>
/// View model for the Configurações screen (issue #52, ERS-Configuracoes.md): expediente
/// (RF-002), provider connection state (RF-001/RF-007), local backup (RF-003/RF-004), the general
/// consolidated CSV export (RF-016, reached from here per the ERS's own 1.1 revision note) and
/// "Sobre" (RF-005/RF-006). Deliberately does not invent new business rules - every action here
/// delegates to an existing Application port/use case (<see cref="IWorkScheduleStore"/>,
/// <see cref="DisconnectProviderUseCase"/>, <see cref="ReconnectProviderUseCase"/>,
/// <see cref="IBackupService"/>, <see cref="GenerateTaskReportUseCase"/>), same convention as every
/// other screen view model in this app.
///
/// "Qual provedor está conectado hoje" reuses <see cref="OnboardingViewModel.ConnectedProviderSettingKey"/>/
/// <see cref="OnboardingViewModel.ProviderDisplayNames"/> as-is (both made <see langword="internal"/>
/// for exactly this reuse) instead of duplicating that lookup - same pattern already used by
/// <c>TarefasViewModel.SyncAsync</c> for the setting key.
/// </summary>
public sealed class ConfiguracoesViewModel : ObservableObject
{
    private readonly IWorkScheduleStore _workScheduleStore;
    private readonly IAppSettingsStore _appSettingsStore;
    private readonly ICredentialStore _credentialStore;
    private readonly IReadOnlyDictionary<string, IProviderAuthenticator> _authenticatorsByProviderId;
    private readonly DisconnectProviderUseCase _disconnectProviderUseCase;
    private readonly ReconnectProviderUseCase _reconnectProviderUseCase;
    private readonly IBackupService _backupService;
    private readonly IBackupFileDialog _backupFileDialog;
    private readonly GenerateTaskReportUseCase _generateTaskReportUseCase;
    private readonly IReportFileSaveDialog _reportFileSaveDialog;

    private string? _connectedProviderId;

    // Expediente (RF-002) - sensible defaults (08:00-18:00, almoço 12:00-13:00, dias úteis)
    // matching the approved prototype's own initial state, used until a saved WorkSchedule is
    // loaded (LoadAsync) or none exists yet.
    private TimeSpan _workStart = TimeSpan.FromHours(8);
    private TimeSpan _workEnd = TimeSpan.FromHours(18);
    private bool _hasLunchBreak = true;
    private TimeSpan _lunchStart = TimeSpan.FromHours(12);
    private TimeSpan _lunchEnd = TimeSpan.FromHours(13);
    private string? _scheduleErrorMessage;

    // Provedores (RF-001/RF-007)
    private bool _hasConnectedProvider;
    private string _connectedProviderDisplayName = string.Empty;
    private bool _isProviderFrozen;
    private bool _isDisconnectConfirmOpen;
    private string? _providerErrorMessage;

    // Backup (RF-003/RF-004)
    private bool _isImportConfirmOpen;
    private string? _backupMessage;
    private bool _isBackupError;

    // Exportar CSV geral (RF-016). CsvFromDate/CsvToDate are plain (non-nullable) DateTime, not
    // DateTime? - MAUI's DatePicker.Date is itself non-nullable, and two-way binding it straight
    // to a nullable property fails the very first time the binding engine pushes the initial
    // null source value into the target (Convert.ChangeType(null, typeof(DateTime)) throws).
    // UsePeriodFilter is the actual "is this filter active" flag (CA-016.3's "filtro opcional") -
    // the two dates are only read when it is on (see ExportGeneralCsvAsync).
    private bool _usePeriodFilter;
    private DateTime _csvFromDate = DateTime.Today.AddDays(-30);
    private DateTime _csvToDate = DateTime.Today;
    private string? _csvErrorMessage;

    public ConfiguracoesViewModel(
        IWorkScheduleStore workScheduleStore,
        IAppSettingsStore appSettingsStore,
        ICredentialStore credentialStore,
        IEnumerable<IProviderAuthenticator> providerAuthenticators,
        DisconnectProviderUseCase disconnectProviderUseCase,
        ReconnectProviderUseCase reconnectProviderUseCase,
        IBackupService backupService,
        IBackupFileDialog backupFileDialog,
        GenerateTaskReportUseCase generateTaskReportUseCase,
        IReportFileSaveDialog reportFileSaveDialog,
        string appVersion)
    {
        _workScheduleStore = workScheduleStore;
        _appSettingsStore = appSettingsStore;
        _credentialStore = credentialStore;
        _authenticatorsByProviderId = providerAuthenticators.ToDictionary(a => a.ProviderId);
        _disconnectProviderUseCase = disconnectProviderUseCase;
        _reconnectProviderUseCase = reconnectProviderUseCase;
        _backupService = backupService;
        _backupFileDialog = backupFileDialog;
        _generateTaskReportUseCase = generateTaskReportUseCase;
        _reportFileSaveDialog = reportFileSaveDialog;

        AppVersionLabel = appVersion;
        OsUserLabel = Environment.UserName;

        WorkDays =
        [
            new WorkDayOption(DayOfWeek.Sunday, "D", isSelected: false),
            new WorkDayOption(DayOfWeek.Monday, "S", isSelected: true),
            new WorkDayOption(DayOfWeek.Tuesday, "T", isSelected: true),
            new WorkDayOption(DayOfWeek.Wednesday, "Q", isSelected: true),
            new WorkDayOption(DayOfWeek.Thursday, "Q", isSelected: true),
            new WorkDayOption(DayOfWeek.Friday, "S", isSelected: true),
            new WorkDayOption(DayOfWeek.Saturday, "S", isSelected: false),
        ];

        SaveScheduleCommand = new AsyncRelayCommand(_ => SaveScheduleAsync(CancellationToken.None));

        OpenDisconnectConfirmCommand = new RelayCommand(_ => OpenDisconnectConfirm());
        CancelDisconnectCommand = new RelayCommand(_ => IsDisconnectConfirmOpen = false);
        ConfirmDisconnectCommand = new AsyncRelayCommand(_ => ConfirmDisconnectAsync(CancellationToken.None));
        ReconnectCommand = new AsyncRelayCommand(_ => ReconnectAsync(CancellationToken.None));

        ExportBackupCommand = new AsyncRelayCommand(_ => ExportBackupAsync(CancellationToken.None));
        OpenImportConfirmCommand = new RelayCommand(_ => OpenImportConfirm());
        CancelImportCommand = new RelayCommand(_ => IsImportConfirmOpen = false);
        ConfirmImportCommand = new AsyncRelayCommand(_ => ConfirmImportAsync(CancellationToken.None));

        ExportGeneralCsvCommand = new AsyncRelayCommand(_ => ExportGeneralCsvAsync(CancellationToken.None));
    }

    // ---- Expediente ----

    public ObservableCollection<WorkDayOption> WorkDays { get; }

    public TimeSpan WorkStart
    {
        get => _workStart;
        set => SetProperty(ref _workStart, value);
    }

    public TimeSpan WorkEnd
    {
        get => _workEnd;
        set => SetProperty(ref _workEnd, value);
    }

    public bool HasLunchBreak
    {
        get => _hasLunchBreak;
        set => SetProperty(ref _hasLunchBreak, value);
    }

    public TimeSpan LunchStart
    {
        get => _lunchStart;
        set => SetProperty(ref _lunchStart, value);
    }

    public TimeSpan LunchEnd
    {
        get => _lunchEnd;
        set => SetProperty(ref _lunchEnd, value);
    }

    /// <summary>Non-null after <see cref="SaveScheduleCommand"/> rejects an invalid schedule (CA-002.2) - e.g. término antes do início, ou almoço fora do expediente.</summary>
    public string? ScheduleErrorMessage
    {
        get => _scheduleErrorMessage;
        private set
        {
            if (SetProperty(ref _scheduleErrorMessage, value))
            {
                OnPropertyChanged(nameof(HasScheduleError));
            }
        }
    }

    public bool HasScheduleError => ScheduleErrorMessage is not null;

    public AsyncRelayCommand SaveScheduleCommand { get; }

    // ---- Provedores ----

    /// <summary>True once a provider was connected at least once (per <c>OnboardingViewModel.ConnectedProviderSettingKey</c>) - should always be true by the time this screen is reachable (RN-001 blocks the whole shell otherwise), kept defensive rather than assumed.</summary>
    public bool HasConnectedProvider
    {
        get => _hasConnectedProvider;
        private set
        {
            if (SetProperty(ref _hasConnectedProvider, value))
            {
                OnPropertyChanged(nameof(NoConnectedProvider));
                OnPropertyChanged(nameof(ShowDisconnectButton));
                OnPropertyChanged(nameof(ShowReconnectButton));
            }
        }
    }

    /// <summary>Inverse of <see cref="HasConnectedProvider"/>, exposed only so the view's "nenhum provedor conectado" label can bind directly without a bool-inverting converter - same convention as <c>DashboardViewModel.NoSelection</c>.</summary>
    public bool NoConnectedProvider => !HasConnectedProvider;

    public string ConnectedProviderDisplayName
    {
        get => _connectedProviderDisplayName;
        private set => SetProperty(ref _connectedProviderDisplayName, value);
    }

    /// <summary>True while the connected provider is frozen (RF-013/RN-008: explicit disconnect or a detected external revocation) - gates <see cref="ShowReconnectButton"/> vs <see cref="ShowDisconnectButton"/>.</summary>
    public bool IsProviderFrozen
    {
        get => _isProviderFrozen;
        private set
        {
            if (SetProperty(ref _isProviderFrozen, value))
            {
                OnPropertyChanged(nameof(ProviderStatusLabel));
                OnPropertyChanged(nameof(ShowDisconnectButton));
                OnPropertyChanged(nameof(ShowReconnectButton));
            }
        }
    }

    public string ProviderStatusLabel => IsProviderFrozen
        ? "Acesso revogado - reconecte para retomar sincronização"
        : "Conectado";

    public bool ShowDisconnectButton => HasConnectedProvider && !IsProviderFrozen;

    public bool ShowReconnectButton => HasConnectedProvider && IsProviderFrozen;

    /// <summary>Gates the "Revogar acesso ao {provedor}?" overlay (mirrors the prototype's <c>showRevokeConfirm</c>/<c>ConcludeTaskModalView</c>'s own IsOpen convention) - no popup/dialog library is available in this app.</summary>
    public bool IsDisconnectConfirmOpen
    {
        get => _isDisconnectConfirmOpen;
        private set => SetProperty(ref _isDisconnectConfirmOpen, value);
    }

    /// <summary>Non-null after a failed <see cref="ReconnectCommand"/>/<see cref="ConfirmDisconnectCommand"/> call.</summary>
    public string? ProviderErrorMessage
    {
        get => _providerErrorMessage;
        private set
        {
            if (SetProperty(ref _providerErrorMessage, value))
            {
                OnPropertyChanged(nameof(HasProviderError));
            }
        }
    }

    public bool HasProviderError => ProviderErrorMessage is not null;

    public RelayCommand OpenDisconnectConfirmCommand { get; }

    public RelayCommand CancelDisconnectCommand { get; }

    public AsyncRelayCommand ConfirmDisconnectCommand { get; }

    public AsyncRelayCommand ReconnectCommand { get; }

    // ---- Backup ----

    /// <summary>Gates the "Importar backup vai substituir os dados atuais" overlay (RN-003: importing overwrites everything, so it gets the same confirm-before-destructive-action treatment as disconnecting a provider).</summary>
    public bool IsImportConfirmOpen
    {
        get => _isImportConfirmOpen;
        private set => SetProperty(ref _isImportConfirmOpen, value);
    }

    /// <summary>Success or error feedback from the last export/import attempt - see <see cref="IsBackupError"/> for which.</summary>
    public string? BackupMessage
    {
        get => _backupMessage;
        private set
        {
            if (SetProperty(ref _backupMessage, value))
            {
                OnPropertyChanged(nameof(ShowBackupSuccess));
                OnPropertyChanged(nameof(ShowBackupError));
            }
        }
    }

    public bool IsBackupError
    {
        get => _isBackupError;
        private set
        {
            if (SetProperty(ref _isBackupError, value))
            {
                OnPropertyChanged(nameof(ShowBackupSuccess));
                OnPropertyChanged(nameof(ShowBackupError));
            }
        }
    }

    /// <summary>True once <see cref="BackupMessage"/> holds a successful export/import result - split from <see cref="ShowBackupError"/> only so the view can style each outcome differently without a bool-to-color converter.</summary>
    public bool ShowBackupSuccess => BackupMessage is not null && !IsBackupError;

    public bool ShowBackupError => BackupMessage is not null && IsBackupError;

    public AsyncRelayCommand ExportBackupCommand { get; }

    public RelayCommand OpenImportConfirmCommand { get; }

    public RelayCommand CancelImportCommand { get; }

    public AsyncRelayCommand ConfirmImportCommand { get; }

    // ---- Exportar CSV geral ----

    /// <summary>Turns the optional De/Até period filter on/off (CA-016.3) - while off, <see cref="ExportGeneralCsvAsync"/> exports every synced task (CA-016.1), ignoring <see cref="CsvFromDate"/>/<see cref="CsvToDate"/>.</summary>
    public bool UsePeriodFilter
    {
        get => _usePeriodFilter;
        set => SetProperty(ref _usePeriodFilter, value);
    }

    public DateTime CsvFromDate
    {
        get => _csvFromDate;
        set => SetProperty(ref _csvFromDate, value);
    }

    public DateTime CsvToDate
    {
        get => _csvToDate;
        set => SetProperty(ref _csvToDate, value);
    }

    /// <summary>Non-null when <see cref="CsvFromDate"/>/<see cref="CsvToDate"/> form an invalid range (CA-016.3-adjacent client-side check) - <see cref="ExportGeneralCsvCommand"/> refuses to run <see cref="GenerateTaskReportUseCase"/> while this is set.</summary>
    public string? CsvErrorMessage
    {
        get => _csvErrorMessage;
        private set
        {
            if (SetProperty(ref _csvErrorMessage, value))
            {
                OnPropertyChanged(nameof(HasCsvError));
            }
        }
    }

    public bool HasCsvError => CsvErrorMessage is not null;

    public AsyncRelayCommand ExportGeneralCsvCommand { get; }

    // ---- Sobre ----

    public string AppVersionLabel { get; }

    /// <summary>RF-005/RN-004: the OS user currently logged in - read directly via <see cref="Environment.UserName"/> (BCL, no platform-specific port needed), same rationale as this project's other direct-BCL reads (e.g. dates/GUIDs elsewhere).</summary>
    public string OsUserLabel { get; }

    /// <summary>Loads the saved <c>WorkSchedule</c> and current provider connection state from local data. Called once when the page appears (<c>ConfiguracoesPage.OnLoaded</c>), same convention as <c>DashboardViewModel</c>/<c>TarefasViewModel</c>.</summary>
    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        await LoadScheduleAsync(cancellationToken);
        await LoadProviderStateAsync(cancellationToken);
    }

    private async Task LoadScheduleAsync(CancellationToken cancellationToken)
    {
        WorkSchedule? schedule = await _workScheduleStore.GetAsync(cancellationToken);
        if (schedule is null)
        {
            return;
        }

        WorkStart = schedule.StartTime.ToTimeSpan();
        WorkEnd = schedule.EndTime.ToTimeSpan();
        HasLunchBreak = schedule.LunchStart.HasValue && schedule.LunchEnd.HasValue;
        if (schedule.LunchStart.HasValue)
        {
            LunchStart = schedule.LunchStart.Value.ToTimeSpan();
        }

        if (schedule.LunchEnd.HasValue)
        {
            LunchEnd = schedule.LunchEnd.Value.ToTimeSpan();
        }

        foreach (WorkDayOption option in WorkDays)
        {
            option.IsSelected = schedule.WorkDays.Contains(option.Day);
        }
    }

    /// <summary>
    /// Builds a <c>WorkSchedule</c> from the current form state and saves it (CA-002.1), or - on an
    /// invalid combination - surfaces <see cref="ScheduleErrorMessage"/> (CA-002.2) without saving.
    /// Deliberately reuses <c>WorkSchedule.Create</c>'s own validation (end after start, lunch
    /// within the expedient, at least one work day) instead of duplicating those rules here.
    /// </summary>
    private async Task SaveScheduleAsync(CancellationToken cancellationToken)
    {
        ScheduleErrorMessage = null;

        List<DayOfWeek> selectedDays = WorkDays.Where(d => d.IsSelected).Select(d => d.Day).ToList();

        try
        {
            WorkSchedule schedule = WorkSchedule.Create(
                TimeOnly.FromTimeSpan(WorkStart),
                TimeOnly.FromTimeSpan(WorkEnd),
                HasLunchBreak ? TimeOnly.FromTimeSpan(LunchStart) : null,
                HasLunchBreak ? TimeOnly.FromTimeSpan(LunchEnd) : null,
                selectedDays);

            await _workScheduleStore.SaveAsync(schedule, cancellationToken);
        }
        catch (ArgumentException ex)
        {
            ScheduleErrorMessage = ex.Message;
        }
    }

    private async Task LoadProviderStateAsync(CancellationToken cancellationToken)
    {
        _connectedProviderId = await _appSettingsStore.GetAsync(
            OnboardingViewModel.ConnectedProviderSettingKey, cancellationToken);

        HasConnectedProvider = _connectedProviderId is not null;
        ConnectedProviderDisplayName = _connectedProviderId is { } id
            ? OnboardingViewModel.ProviderDisplayNames.GetValueOrDefault(id, id)
            : string.Empty;

        IsProviderFrozen = _connectedProviderId is { } providerId
            && await _appSettingsStore.GetAsync(ProviderSettingsKeys.Frozen(providerId), cancellationToken) == "true";
    }

    private void OpenDisconnectConfirm()
    {
        ProviderErrorMessage = null;
        IsDisconnectConfirmOpen = true;
    }

    private async Task ConfirmDisconnectAsync(CancellationToken cancellationToken)
    {
        IsDisconnectConfirmOpen = false;

        if (_connectedProviderId is not { } providerId)
        {
            return;
        }

        try
        {
            await _disconnectProviderUseCase.ExecuteAsync(providerId, cancellationToken);
            await LoadProviderStateAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            ProviderErrorMessage = ex.Message;
        }
    }

    /// <summary>
    /// Reconnects the current provider (RF-007/RF-013's "até a reconexão"): re-authenticates via
    /// the same <see cref="IProviderAuthenticator"/> onboarding uses (RF-001 - no manual
    /// credentials), saves the fresh token, then unfreezes and re-syncs via
    /// <see cref="ReconnectProviderUseCase"/>. A fresh authentication is required here (not just
    /// calling <see cref="ReconnectProviderUseCase"/> directly): <see cref="DisconnectProviderUseCase"/>
    /// deletes the saved token outright, and an externally-revoked token
    /// (<c>GitHubProjectsClient</c>'s 401 handling) is no longer valid at the provider either - so
    /// in both cases the old token cannot carry a reconnect on its own.
    /// </summary>
    private async Task ReconnectAsync(CancellationToken cancellationToken)
    {
        ProviderErrorMessage = null;

        if (_connectedProviderId is not { } providerId)
        {
            return;
        }

        if (!_authenticatorsByProviderId.TryGetValue(providerId, out IProviderAuthenticator? authenticator))
        {
            ProviderErrorMessage = $"Nenhum autenticador registrado para o provedor '{providerId}'.";
            return;
        }

        try
        {
            ProviderAuthResult authResult = await authenticator.AuthenticateAsync(cancellationToken);
            await _credentialStore.SaveAsync(ProviderSettingsKeys.CredentialKey(providerId), authResult.AccessToken, cancellationToken);

            await _reconnectProviderUseCase.ExecuteAsync(providerId, cancellationToken);
            await LoadProviderStateAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            ProviderErrorMessage = ex.Message;
        }
    }

    private async Task ExportBackupAsync(CancellationToken cancellationToken)
    {
        BackupMessage = null;
        IsBackupError = false;

        var path = _backupFileDialog.RequestExportPath($"taskengine-backup-{DateTime.Now:yyyyMMdd}.zip");
        if (path is null)
        {
            return;
        }

        try
        {
            await _backupService.ExportAsync(path, cancellationToken);
            BackupMessage = "Backup exportado com sucesso.";
        }
        catch (Exception ex)
        {
            IsBackupError = true;
            BackupMessage = ex.Message;
        }
    }

    private void OpenImportConfirm()
    {
        BackupMessage = null;
        IsBackupError = false;
        IsImportConfirmOpen = true;
    }

    /// <summary>
    /// Imports a backup (RF-004/RN-003: full overwrite, no merge). CA-004.2 ("arquivo inválido ->
    /// rejeita e mantém os dados atuais") is enforced by <c>IBackupService.ImportAsync</c> itself
    /// (manifest/schema-version validation before touching the live database) - any failure there
    /// surfaces as an exception, caught here without having modified anything.
    /// </summary>
    private async Task ConfirmImportAsync(CancellationToken cancellationToken)
    {
        IsImportConfirmOpen = false;

        var path = _backupFileDialog.RequestImportPath();
        if (path is null)
        {
            return;
        }

        try
        {
            await _backupService.ImportAsync(path, cancellationToken);

            // RN-002: credentials are never part of a backup, so whichever provider the imported
            // data points at still needs a fresh connection here before it can sync/complete
            // anything again (EVT-003).
            BackupMessage = "Backup importado com sucesso. Reconecte o provedor antes de sincronizar ou concluir tarefas.";
            await LoadAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            IsBackupError = true;
            BackupMessage = ex.Message;
        }
    }

    /// <summary>Client-side check backing <see cref="CsvErrorMessage"/> - pure and static so it's directly unit-testable without any I/O.</summary>
    public static bool TryValidateDateRange(DateTime? from, DateTime? to, out string? errorMessage)
    {
        if (from is { } fromValue && to is { } toValue && DateOnly.FromDateTime(toValue) < DateOnly.FromDateTime(fromValue))
        {
            errorMessage = "A data 'Até' não pode ser anterior à data 'De'.";
            return false;
        }

        errorMessage = null;
        return true;
    }

    /// <summary>Exports the general consolidated report (RF-016) as CSV, honoring the optional De/Até period filter (CA-016.3) - same <see cref="GenerateTaskReportUseCase"/>/<see cref="CsvTaskReportWriter"/>/<see cref="IReportFileSaveDialog"/> pipeline <c>RelatorioViewModel</c> uses for its own (per-task) export.</summary>
    private async Task ExportGeneralCsvAsync(CancellationToken cancellationToken)
    {
        CsvErrorMessage = null;

        DateTime? from = UsePeriodFilter ? CsvFromDate : null;
        DateTime? to = UsePeriodFilter ? CsvToDate : null;

        if (!TryValidateDateRange(from, to, out var validationError))
        {
            CsvErrorMessage = validationError;
            return;
        }

        DateOnly? periodStart = from is { } fromValue ? DateOnly.FromDateTime(fromValue) : null;
        DateOnly? periodEnd = to is { } toValue ? DateOnly.FromDateTime(toValue) : null;

        IReadOnlyList<TaskReportRow> rows =
            await _generateTaskReportUseCase.ExecuteAsync(periodStart, periodEnd, cancellationToken);

        using Stream? stream = _reportFileSaveDialog.RequestSaveStream($"taskengine-relatorio-geral-{DateTime.Now:yyyyMMdd}.csv");
        if (stream is null)
        {
            return;
        }

        using var writer = new StreamWriter(stream, Encoding.UTF8);
        CsvTaskReportWriter.Write(rows, writer);
    }
}
