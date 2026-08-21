using TaskEngine.Application.Abstractions;
using TaskEngine.Application.Providers;
using TaskEngine.Application.Reports;
using TaskEngine.Application.Tasks;
using TaskEngine.Application.WorkSessions;
using TaskEngine.Desktop.ViewModels;
using TaskEngine.Domain.Entities;

namespace TaskEngine.Desktop.Tests;

public class ConfiguracoesViewModelTests
{
    private static readonly ProviderAuthResult SuccessfulAuthResult =
        new("github", "fresh-token", "repo read:project", DateTimeOffset.UtcNow);

    private sealed record Fixture(
        ConfiguracoesViewModel ViewModel,
        FakeWorkScheduleStore WorkScheduleStore,
        FakeAppSettingsStore AppSettingsStore,
        FakeCredentialStore CredentialStore,
        FakeBackupService BackupService,
        FakeBackupFileDialog BackupFileDialog,
        FakeReportFileSaveDialog ReportFileSaveDialog);

    private static Fixture CreateFixture(
        FakeAppSettingsStore? appSettingsStore = null,
        FakeCredentialStore? credentialStore = null,
        FakeProviderAuthenticator? authenticator = null,
        FakeTaskProviderClient? providerClient = null,
        FakeBackupService? backupService = null,
        FakeBackupFileDialog? backupFileDialog = null,
        FakeReportFileSaveDialog? reportFileSaveDialog = null)
    {
        var taskRepository = new FakeTaskRepository();
        var workSessionRepository = new FakeWorkSessionRepository();
        var workScheduleStore = new FakeWorkScheduleStore();
        appSettingsStore ??= new FakeAppSettingsStore();
        credentialStore ??= new FakeCredentialStore();

        var providerAuthenticators = authenticator is null
            ? Array.Empty<IProviderAuthenticator>()
            : [authenticator];

        var disconnectProviderUseCase = new DisconnectProviderUseCase(credentialStore, appSettingsStore);

        var providerClientFactory = providerClient is null
            ? new FakeProviderClientFactory()
            : new FakeProviderClientFactory(providerClient);
        var startWorkSessionUseCase = new StartWorkSessionUseCase(taskRepository, workSessionRepository);
        var pauseWorkSessionUseCase = new PauseWorkSessionUseCase(taskRepository, workSessionRepository);
        var endWorkSessionUseCase = new EndWorkSessionUseCase(taskRepository, workSessionRepository);
        var syncTasksUseCase = new SyncTasksUseCase(
            taskRepository, providerClientFactory, appSettingsStore,
            startWorkSessionUseCase, pauseWorkSessionUseCase, endWorkSessionUseCase);
        var reconnectProviderUseCase = new ReconnectProviderUseCase(
            appSettingsStore, taskRepository, workSessionRepository, providerClientFactory, syncTasksUseCase);

        backupService ??= new FakeBackupService();
        backupFileDialog ??= new FakeBackupFileDialog();

        var unmappedTimeEntryRepository = new FakeUnmappedTimeEntryRepository();
        var generateTaskReportUseCase = new GenerateTaskReportUseCase(
            taskRepository, workSessionRepository, workScheduleStore, unmappedTimeEntryRepository);
        reportFileSaveDialog ??= new FakeReportFileSaveDialog();

        var viewModel = new ConfiguracoesViewModel(
            workScheduleStore,
            appSettingsStore,
            credentialStore,
            providerAuthenticators,
            disconnectProviderUseCase,
            reconnectProviderUseCase,
            backupService,
            backupFileDialog,
            generateTaskReportUseCase,
            reportFileSaveDialog,
            appVersion: "1.0");

        return new Fixture(
            viewModel, workScheduleStore, appSettingsStore, credentialStore, backupService, backupFileDialog, reportFileSaveDialog);
    }

    // ---- Sobre ----

    [Fact]
    public void Constructor_ExposesAppVersionAndOsUser()
    {
        Fixture fixture = CreateFixture();

        Assert.Equal("1.0", fixture.ViewModel.AppVersionLabel);
        Assert.Equal(Environment.UserName, fixture.ViewModel.OsUserLabel);
    }

    // ---- Expediente ----

    [Fact]
    public async Task LoadAsync_WithNoSavedSchedule_KeepsDefaultFormState()
    {
        Fixture fixture = CreateFixture();

        await fixture.ViewModel.LoadAsync();

        Assert.Equal(TimeSpan.FromHours(8), fixture.ViewModel.WorkStart);
        Assert.Equal(TimeSpan.FromHours(18), fixture.ViewModel.WorkEnd);
        Assert.True(fixture.ViewModel.HasLunchBreak);
        Assert.True(fixture.ViewModel.WorkDays.First(d => d.Day == DayOfWeek.Monday).IsSelected);
        Assert.False(fixture.ViewModel.WorkDays.First(d => d.Day == DayOfWeek.Sunday).IsSelected);
    }

    [Fact]
    public async Task LoadAsync_WithSavedSchedule_PopulatesFormFromIt()
    {
        Fixture fixture = CreateFixture();
        WorkSchedule schedule = WorkSchedule.Create(
            new TimeOnly(9, 0), new TimeOnly(17, 0), null, null, [DayOfWeek.Tuesday, DayOfWeek.Thursday]);
        await fixture.WorkScheduleStore.SaveAsync(schedule, CancellationToken.None);

        await fixture.ViewModel.LoadAsync();

        Assert.Equal(TimeSpan.FromHours(9), fixture.ViewModel.WorkStart);
        Assert.Equal(TimeSpan.FromHours(17), fixture.ViewModel.WorkEnd);
        Assert.False(fixture.ViewModel.HasLunchBreak);
        Assert.True(fixture.ViewModel.WorkDays.First(d => d.Day == DayOfWeek.Tuesday).IsSelected);
        Assert.False(fixture.ViewModel.WorkDays.First(d => d.Day == DayOfWeek.Monday).IsSelected);
    }

    [Fact]
    public async Task SaveScheduleCommand_WithValidData_PersistsAndClearsError()
    {
        Fixture fixture = CreateFixture();
        fixture.ViewModel.WorkStart = TimeSpan.FromHours(8);
        fixture.ViewModel.WorkEnd = TimeSpan.FromHours(17);
        fixture.ViewModel.HasLunchBreak = false;

        await fixture.ViewModel.SaveScheduleCommand.ExecuteAsync(null);

        Assert.False(fixture.ViewModel.HasScheduleError);
        WorkSchedule? saved = await fixture.WorkScheduleStore.GetAsync(CancellationToken.None);
        Assert.NotNull(saved);
        Assert.Equal(new TimeOnly(8, 0), saved!.StartTime);
        Assert.Equal(new TimeOnly(17, 0), saved.EndTime);
        Assert.Null(saved.LunchStart);
    }

    [Fact]
    public async Task SaveScheduleCommand_WithEndBeforeStart_SurfacesErrorAndDoesNotPersist_CA002_2()
    {
        Fixture fixture = CreateFixture();
        fixture.ViewModel.WorkStart = TimeSpan.FromHours(18);
        fixture.ViewModel.WorkEnd = TimeSpan.FromHours(8);

        await fixture.ViewModel.SaveScheduleCommand.ExecuteAsync(null);

        Assert.True(fixture.ViewModel.HasScheduleError);
        Assert.NotNull(fixture.ViewModel.ScheduleErrorMessage);
        Assert.Null(await fixture.WorkScheduleStore.GetAsync(CancellationToken.None));
    }

    [Fact]
    public async Task SaveScheduleCommand_WithNoWorkDaysSelected_SurfacesError_CA002_2()
    {
        Fixture fixture = CreateFixture();
        foreach (WorkDayOption day in fixture.ViewModel.WorkDays)
        {
            day.IsSelected = false;
        }

        await fixture.ViewModel.SaveScheduleCommand.ExecuteAsync(null);

        Assert.True(fixture.ViewModel.HasScheduleError);
        Assert.Null(await fixture.WorkScheduleStore.GetAsync(CancellationToken.None));
    }

    [Fact]
    public async Task SaveScheduleCommand_WithLunchOutsideExpedient_SurfacesError_CA002_2()
    {
        Fixture fixture = CreateFixture();
        fixture.ViewModel.WorkStart = TimeSpan.FromHours(8);
        fixture.ViewModel.WorkEnd = TimeSpan.FromHours(17);
        fixture.ViewModel.HasLunchBreak = true;
        fixture.ViewModel.LunchStart = TimeSpan.FromHours(18);
        fixture.ViewModel.LunchEnd = TimeSpan.FromHours(19);

        await fixture.ViewModel.SaveScheduleCommand.ExecuteAsync(null);

        Assert.True(fixture.ViewModel.HasScheduleError);
        Assert.Null(await fixture.WorkScheduleStore.GetAsync(CancellationToken.None));
    }

    // ---- Provedores ----

    [Fact]
    public async Task LoadAsync_WithNoProviderEverConnected_ShowsNoConnectedProvider()
    {
        Fixture fixture = CreateFixture();

        await fixture.ViewModel.LoadAsync();

        Assert.False(fixture.ViewModel.HasConnectedProvider);
        Assert.True(fixture.ViewModel.NoConnectedProvider);
        Assert.False(fixture.ViewModel.ShowDisconnectButton);
        Assert.False(fixture.ViewModel.ShowReconnectButton);
    }

    [Fact]
    public async Task LoadAsync_WithConnectedUnfrozenProvider_ShowsDisconnectButton()
    {
        var appSettingsStore = new FakeAppSettingsStore();
        await appSettingsStore.SetAsync("provider:connected", "github", CancellationToken.None);
        Fixture fixture = CreateFixture(appSettingsStore: appSettingsStore);

        await fixture.ViewModel.LoadAsync();

        Assert.True(fixture.ViewModel.HasConnectedProvider);
        Assert.Equal("GitHub", fixture.ViewModel.ConnectedProviderDisplayName);
        Assert.False(fixture.ViewModel.IsProviderFrozen);
        Assert.True(fixture.ViewModel.ShowDisconnectButton);
        Assert.False(fixture.ViewModel.ShowReconnectButton);
    }

    [Fact]
    public async Task LoadAsync_WithFrozenProvider_ShowsReconnectButton()
    {
        var appSettingsStore = new FakeAppSettingsStore();
        await appSettingsStore.SetAsync("provider:connected", "github", CancellationToken.None);
        await appSettingsStore.SetAsync(ProviderSettingsKeys.Frozen("github"), "true", CancellationToken.None);
        Fixture fixture = CreateFixture(appSettingsStore: appSettingsStore);

        await fixture.ViewModel.LoadAsync();

        Assert.True(fixture.ViewModel.IsProviderFrozen);
        Assert.False(fixture.ViewModel.ShowDisconnectButton);
        Assert.True(fixture.ViewModel.ShowReconnectButton);
    }

    [Fact]
    public async Task OpenDisconnectConfirmCommand_OpensOverlay_ConfirmDisconnects_CA007_1()
    {
        var appSettingsStore = new FakeAppSettingsStore();
        await appSettingsStore.SetAsync("provider:connected", "github", CancellationToken.None);
        var credentialStore = new FakeCredentialStore();
        await credentialStore.SaveAsync(ProviderSettingsKeys.CredentialKey("github"), "old-token", CancellationToken.None);
        Fixture fixture = CreateFixture(appSettingsStore: appSettingsStore, credentialStore: credentialStore);
        await fixture.ViewModel.LoadAsync();

        fixture.ViewModel.OpenDisconnectConfirmCommand.Execute(null);
        Assert.True(fixture.ViewModel.IsDisconnectConfirmOpen);

        await fixture.ViewModel.ConfirmDisconnectCommand.ExecuteAsync(null);

        Assert.False(fixture.ViewModel.IsDisconnectConfirmOpen);
        Assert.True(fixture.ViewModel.IsProviderFrozen);
        Assert.True(fixture.ViewModel.ShowReconnectButton);
        Assert.Empty(credentialStore.Secrets);
    }

    [Fact]
    public async Task CancelDisconnectCommand_ClosesOverlay_WithoutDisconnecting()
    {
        var appSettingsStore = new FakeAppSettingsStore();
        await appSettingsStore.SetAsync("provider:connected", "github", CancellationToken.None);
        Fixture fixture = CreateFixture(appSettingsStore: appSettingsStore);
        await fixture.ViewModel.LoadAsync();

        fixture.ViewModel.OpenDisconnectConfirmCommand.Execute(null);
        fixture.ViewModel.CancelDisconnectCommand.Execute(null);

        Assert.False(fixture.ViewModel.IsDisconnectConfirmOpen);
        Assert.False(fixture.ViewModel.IsProviderFrozen);
    }

    [Fact]
    public async Task ReconnectCommand_ReauthenticatesSavesTokenAndUnfreezes()
    {
        var appSettingsStore = new FakeAppSettingsStore();
        await appSettingsStore.SetAsync("provider:connected", "github", CancellationToken.None);
        await appSettingsStore.SetAsync(ProviderSettingsKeys.Frozen("github"), "true", CancellationToken.None);
        var credentialStore = new FakeCredentialStore();
        var authenticator = new FakeProviderAuthenticator("github", SuccessfulAuthResult);
        var providerClient = new FakeTaskProviderClient("github", assignedTasks: []);

        Fixture fixture = CreateFixture(
            appSettingsStore: appSettingsStore,
            credentialStore: credentialStore,
            authenticator: authenticator,
            providerClient: providerClient);
        await fixture.ViewModel.LoadAsync();

        await fixture.ViewModel.ReconnectCommand.ExecuteAsync(null);

        Assert.Equal(1, authenticator.AuthenticateCallCount);
        Assert.Equal("fresh-token", credentialStore.Secrets["provider:github:token"]);
        Assert.False(fixture.ViewModel.IsProviderFrozen);
        Assert.False(fixture.ViewModel.HasProviderError);
        Assert.True(fixture.ViewModel.ShowDisconnectButton);
    }

    [Fact]
    public async Task ReconnectCommand_WhenAuthenticationFails_SurfacesError_StaysFrozen()
    {
        var appSettingsStore = new FakeAppSettingsStore();
        await appSettingsStore.SetAsync("provider:connected", "github", CancellationToken.None);
        await appSettingsStore.SetAsync(ProviderSettingsKeys.Frozen("github"), "true", CancellationToken.None);
        var authenticator = new FakeProviderAuthenticator("github", new InvalidOperationException("Login cancelado."));

        Fixture fixture = CreateFixture(appSettingsStore: appSettingsStore, authenticator: authenticator);
        await fixture.ViewModel.LoadAsync();

        await fixture.ViewModel.ReconnectCommand.ExecuteAsync(null);

        Assert.True(fixture.ViewModel.HasProviderError);
        Assert.Equal("Login cancelado.", fixture.ViewModel.ProviderErrorMessage);
        Assert.True(fixture.ViewModel.IsProviderFrozen);
    }

    // ---- Backup ----

    [Fact]
    public async Task ExportBackupCommand_CallsBackupServiceWithDialogPath_ShowsSuccess()
    {
        var backupFileDialog = new FakeBackupFileDialog(exportPath: "C:\\out\\backup.zip");
        var backupService = new FakeBackupService();
        Fixture fixture = CreateFixture(backupFileDialog: backupFileDialog, backupService: backupService);

        await fixture.ViewModel.ExportBackupCommand.ExecuteAsync(null);

        Assert.Equal(1, backupService.ExportCallCount);
        Assert.Equal("C:\\out\\backup.zip", backupService.LastExportedPath);
        Assert.True(fixture.ViewModel.ShowBackupSuccess);
        Assert.False(fixture.ViewModel.ShowBackupError);
    }

    [Fact]
    public async Task ExportBackupCommand_UserCancelsDialog_NeverCallsBackupService()
    {
        var backupFileDialog = new FakeBackupFileDialog(exportPath: null);
        var backupService = new FakeBackupService();
        Fixture fixture = CreateFixture(backupFileDialog: backupFileDialog, backupService: backupService);

        await fixture.ViewModel.ExportBackupCommand.ExecuteAsync(null);

        Assert.Equal(0, backupService.ExportCallCount);
        Assert.Null(fixture.ViewModel.BackupMessage);
    }

    [Fact]
    public async Task ExportBackupCommand_WhenServiceThrows_ShowsError_CA003_2()
    {
        var backupService = new FakeBackupService(exportFailure: new IOException("Disco cheio."));
        Fixture fixture = CreateFixture(backupService: backupService);

        await fixture.ViewModel.ExportBackupCommand.ExecuteAsync(null);

        Assert.True(fixture.ViewModel.ShowBackupError);
        Assert.Equal("Disco cheio.", fixture.ViewModel.BackupMessage);
    }

    [Fact]
    public async Task ConfirmImportCommand_CallsBackupServiceWithDialogPath_ShowsSuccessWithReconnectHint_CA004_1()
    {
        var backupFileDialog = new FakeBackupFileDialog(importPath: "C:\\in\\backup.zip");
        var backupService = new FakeBackupService();
        Fixture fixture = CreateFixture(backupFileDialog: backupFileDialog, backupService: backupService);

        fixture.ViewModel.OpenImportConfirmCommand.Execute(null);
        Assert.True(fixture.ViewModel.IsImportConfirmOpen);

        await fixture.ViewModel.ConfirmImportCommand.ExecuteAsync(null);

        Assert.False(fixture.ViewModel.IsImportConfirmOpen);
        Assert.Equal(1, backupService.ImportCallCount);
        Assert.Equal("C:\\in\\backup.zip", backupService.LastImportedPath);
        Assert.True(fixture.ViewModel.ShowBackupSuccess);
        Assert.Contains("Reconecte", fixture.ViewModel.BackupMessage);
    }

    [Fact]
    public async Task ConfirmImportCommand_UserCancelsDialog_NeverCallsBackupService()
    {
        var backupFileDialog = new FakeBackupFileDialog(importPath: null);
        var backupService = new FakeBackupService();
        Fixture fixture = CreateFixture(backupFileDialog: backupFileDialog, backupService: backupService);

        fixture.ViewModel.OpenImportConfirmCommand.Execute(null);
        await fixture.ViewModel.ConfirmImportCommand.ExecuteAsync(null);

        Assert.Equal(0, backupService.ImportCallCount);
    }

    [Fact]
    public async Task ConfirmImportCommand_WhenBackupIsInvalid_ShowsError_KeepsLocalDataIntact_CA004_2()
    {
        var backupService = new FakeBackupService(
            importFailure: new InvalidOperationException("Cannot restore this backup: incompatible schema version."));
        Fixture fixture = CreateFixture(backupService: backupService);

        fixture.ViewModel.OpenImportConfirmCommand.Execute(null);
        await fixture.ViewModel.ConfirmImportCommand.ExecuteAsync(null);

        Assert.True(fixture.ViewModel.ShowBackupError);
        Assert.Contains("incompatible schema version", fixture.ViewModel.BackupMessage);
    }

    [Fact]
    public void CancelImportCommand_ClosesOverlay_WithoutImporting()
    {
        Fixture fixture = CreateFixture();

        fixture.ViewModel.OpenImportConfirmCommand.Execute(null);
        fixture.ViewModel.CancelImportCommand.Execute(null);

        Assert.False(fixture.ViewModel.IsImportConfirmOpen);
    }

    // ---- Exportar CSV geral ----

    [Fact]
    public void TryValidateDateRange_WithToBeforeFrom_ReturnsFalseWithMessage()
    {
        var isValid = ConfiguracoesViewModel.TryValidateDateRange(
            new DateTime(2026, 2, 10), new DateTime(2026, 1, 1), out var error);

        Assert.False(isValid);
        Assert.False(string.IsNullOrEmpty(error));
    }

    [Fact]
    public void TryValidateDateRange_WithFromBeforeOrEqualTo_ReturnsTrue()
    {
        Assert.True(ConfiguracoesViewModel.TryValidateDateRange(
            new DateTime(2026, 1, 1), new DateTime(2026, 1, 31), out var error1));
        Assert.Null(error1);

        Assert.True(ConfiguracoesViewModel.TryValidateDateRange(
            new DateTime(2026, 1, 1), new DateTime(2026, 1, 1), out var error2));
        Assert.Null(error2);
    }

    [Fact]
    public void TryValidateDateRange_WithEitherSideMissing_ReturnsTrue()
    {
        Assert.True(ConfiguracoesViewModel.TryValidateDateRange(null, new DateTime(2026, 1, 1), out _));
        Assert.True(ConfiguracoesViewModel.TryValidateDateRange(new DateTime(2026, 1, 1), null, out _));
        Assert.True(ConfiguracoesViewModel.TryValidateDateRange(null, null, out _));
    }

    [Fact]
    public async Task ExportGeneralCsvCommand_WithFilterOffAndInvalidDatesLoaded_StillExports_CA016_1()
    {
        // UsePeriodFilter is off by default - an invalid CsvFromDate/CsvToDate combination must not
        // block the export, since it is not actually applied (CA-016.1: every synced task).
        var saveDialog = new FakeReportFileSaveDialog();
        Fixture fixture = CreateFixture(reportFileSaveDialog: saveDialog);
        fixture.ViewModel.CsvFromDate = new DateTime(2026, 12, 31);
        fixture.ViewModel.CsvToDate = new DateTime(2026, 1, 1);

        await fixture.ViewModel.ExportGeneralCsvCommand.ExecuteAsync(null);

        Assert.False(fixture.ViewModel.HasCsvError);
        Assert.Equal(1, saveDialog.CallCount);
    }

    [Fact]
    public async Task ExportGeneralCsvCommand_WithFilterOnAndInvalidRange_SurfacesError_NeverOpensDialog()
    {
        var saveDialog = new FakeReportFileSaveDialog();
        Fixture fixture = CreateFixture(reportFileSaveDialog: saveDialog);
        fixture.ViewModel.UsePeriodFilter = true;
        fixture.ViewModel.CsvFromDate = new DateTime(2026, 12, 31);
        fixture.ViewModel.CsvToDate = new DateTime(2026, 1, 1);

        await fixture.ViewModel.ExportGeneralCsvCommand.ExecuteAsync(null);

        Assert.True(fixture.ViewModel.HasCsvError);
        Assert.Equal(0, saveDialog.CallCount);
    }

    [Fact]
    public async Task ExportGeneralCsvCommand_WithValidFilter_WritesCsvViaSaveDialog()
    {
        var saveDialog = new FakeReportFileSaveDialog();
        Fixture fixture = CreateFixture(reportFileSaveDialog: saveDialog);
        fixture.ViewModel.UsePeriodFilter = true;
        fixture.ViewModel.CsvFromDate = new DateTime(2026, 1, 1);
        fixture.ViewModel.CsvToDate = new DateTime(2026, 1, 31);

        await fixture.ViewModel.ExportGeneralCsvCommand.ExecuteAsync(null);

        Assert.False(fixture.ViewModel.HasCsvError);
        Assert.Equal(1, saveDialog.CallCount);
        Assert.Contains("id,inicio,fim,provedor", saveDialog.CapturedCsv);
    }

    [Fact]
    public async Task ExportGeneralCsvCommand_UserCancelsDialog_DoesNotThrow()
    {
        var saveDialog = new FakeReportFileSaveDialog(cancels: true);
        Fixture fixture = CreateFixture(reportFileSaveDialog: saveDialog);

        Exception? exception = await Record.ExceptionAsync(() => fixture.ViewModel.ExportGeneralCsvCommand.ExecuteAsync(null));

        Assert.Null(exception);
        Assert.Equal(1, saveDialog.CallCount);
        Assert.Null(saveDialog.CapturedCsv);
    }
}
