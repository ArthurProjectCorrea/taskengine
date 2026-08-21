namespace TaskEngine.Desktop.ViewModels.LocalBackup;

/// <summary>
/// Port (issue #52, RF-003/RF-004, ERS-Configuracoes.md) for showing the OS's native "save"/"open"
/// file dialogs for the backup export/import flow, returning the file path the user picked -
/// implemented in TaskEngine.Desktop's <c>Platforms/Windows/</c> via the native Win32 common
/// dialogs (<c>System.Windows.Forms.SaveFileDialog</c>/<c>OpenFileDialog</c>), the same
/// <c>FrameworkReference</c> mechanism <see cref="Reports.IReportFileSaveDialog"/> already uses -
/// no third-party dialog/file-picker package added.
///
/// Returns a path here (not a <see cref="Stream"/> like <see cref="Reports.IReportFileSaveDialog"/>):
/// <c>IBackupService</c> (TaskEngine.Application.Abstractions) works with file paths directly
/// (<c>ExportAsync(string destinationZipPath, ...)</c>/<c>ImportAsync(string sourceZipPath, ...)</c>,
/// since it needs to VACUUM INTO/zip/extract real files on disk) rather than an already-open
/// stream, so a stream-returning dialog would not fit here without an awkward temp-file round trip.
///
/// Both members return <see langword="null"/> - and never touch the filesystem beyond the dialog
/// itself - if the user cancels; callers must treat that as a silent no-op, not an error, same
/// convention as <see cref="Reports.IReportFileSaveDialog.RequestSaveStream"/>.
///
/// Namespace/folder is "LocalBackup", not "Backup": this repo's root <c>.gitignore</c> has a
/// boilerplate Visual-Studio-upgrade rule (<c>Backup*/</c>) that silently swallows any
/// <c>Backup/</c> folder anywhere in the tree - discovered when a plain <c>Backup/</c> folder here
/// never showed up in <c>git status</c>. Avoided rather than special-cased in <c>.gitignore</c>.
/// </summary>
public interface IBackupFileDialog
{
    string? RequestExportPath(string suggestedFileName);

    string? RequestImportPath();
}
