namespace TaskEngine.Desktop.ViewModels.Reports;

/// <summary>
/// Port (plan item 9) for showing the OS's native "save file" dialog and returning a writable
/// stream to the destination the user picked - implemented in TaskEngine.Desktop's
/// <c>Platforms/Windows/</c> via the native Win32 common "Save As" dialog
/// (<c>System.Windows.Forms.SaveFileDialog</c>, already available in this project through the
/// WindowsForms <c>FrameworkReference</c> used by <c>TrayIconService</c>'s <c>NotifyIcon</c> - no
/// third-party dialog/file-picker package added). Kept in this MAUI-free project (not
/// <c>TaskEngine.Desktop</c>) so <c>RelatorioViewModel</c> stays testable with an in-memory fake.
///
/// Returns <see langword="null"/> - and never touches the filesystem - if the user cancels the
/// dialog; callers must treat that as a silent no-op, not an error.
/// </summary>
public interface IReportFileSaveDialog
{
    Stream? RequestSaveStream(string suggestedFileName);
}
