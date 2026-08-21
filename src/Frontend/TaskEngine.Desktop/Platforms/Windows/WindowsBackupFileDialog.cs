using System.Windows.Forms;
using TaskEngine.Desktop.ViewModels.LocalBackup;

namespace TaskEngine.Desktop.Platforms.Windows;

/// <summary>
/// Windows implementation of <see cref="IBackupFileDialog"/> (issue #52, RF-003/RF-004): shows the
/// native Win32 "Salvar como"/"Abrir" common dialogs via <see cref="SaveFileDialog"/>/
/// <see cref="OpenFileDialog"/> - same <c>System.Windows.Forms</c> <c>FrameworkReference</c> already
/// used by <see cref="WindowsReportFileSaveDialog"/>/<see cref="TrayIconService"/>, not a
/// third-party dependency. See <see cref="IBackupFileDialog"/>'s own doc comment for why this
/// returns a path instead of a stream.
/// </summary>
internal sealed class WindowsBackupFileDialog : IBackupFileDialog
{
    private const string ZipFilter = "Backup TaskEngine (*.zip)|*.zip";

    public string? RequestExportPath(string suggestedFileName)
    {
        using var dialog = new SaveFileDialog
        {
            FileName = suggestedFileName,
            Filter = ZipFilter,
            DefaultExt = "zip",
            AddExtension = true,
        };

        return ShowDialog(dialog) == DialogResult.OK ? dialog.FileName : null;
    }

    public string? RequestImportPath()
    {
        using var dialog = new OpenFileDialog
        {
            Filter = ZipFilter,
            DefaultExt = "zip",
            CheckFileExists = true,
            Multiselect = false,
        };

        return ShowDialog(dialog) == DialogResult.OK ? dialog.FileName : null;
    }

    private static DialogResult ShowDialog(CommonDialog dialog)
    {
        var owner = new Win32Window(MainWindowManager.GetWindowHandle());
        return dialog switch
        {
            FileDialog fileDialog => owner.Handle == IntPtr.Zero ? fileDialog.ShowDialog() : fileDialog.ShowDialog(owner),
            _ => throw new NotSupportedException($"Unsupported dialog type '{dialog.GetType()}'."),
        };
    }

    /// <summary>Minimal <see cref="IWin32Window"/> wrapper around a raw HWND - same as <c>WindowsReportFileSaveDialog.Win32Window</c>.</summary>
    private sealed class Win32Window : IWin32Window
    {
        public Win32Window(IntPtr handle)
        {
            Handle = handle;
        }

        public IntPtr Handle { get; }
    }
}
