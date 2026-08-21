namespace TaskEngine.Desktop.ViewModels.Providers;

/// <summary>
/// Port for opening a task's provider page in the user's default browser (RF-012/CA-012.1,
/// ERS-Tarefas.md) - implemented in TaskEngine.Desktop's <c>Platforms/Windows/</c> via
/// <c>Process.Start</c> with <c>UseShellExecute = true</c>, same rationale as
/// <see cref="TaskEngine.Desktop.ViewModels.Reports.IReportFileSaveDialog"/>/
/// <see cref="TaskEngine.Desktop.ViewModels.LocalBackup.IBackupFileDialog"/>: opening a URL is an
/// OS-level concern, kept out of this MAUI-free project's view models directly so they stay
/// testable with an in-memory fake.
/// </summary>
public interface IProviderLinkOpener
{
    void Open(string url);
}
