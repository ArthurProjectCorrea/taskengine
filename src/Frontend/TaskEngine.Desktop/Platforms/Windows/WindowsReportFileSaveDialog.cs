using System.Windows.Forms;
using TaskEngine.Desktop.ViewModels.Reports;

namespace TaskEngine.Desktop.Platforms.Windows;

/// <summary>
/// Windows implementation of <see cref="IReportFileSaveDialog"/> (plan item 9, RF-011): shows the
/// native Win32 "Salvar como" common dialog via <see cref="SaveFileDialog"/>. Not a third-party
/// dependency - <c>System.Windows.Forms</c> is already available in this project through the
/// WindowsForms <c>FrameworkReference</c> added for <see cref="TrayIconService"/>'s
/// <c>NotifyIcon</c> (see <c>TaskEngine.Desktop.csproj</c>'s own comment on that choice).
///
/// This project has no equivalent of .NET MAUI's own file pickers for "save" (only
/// <c>FilePicker</c>/<c>FolderPicker</c> exist in <c>Microsoft.Maui.Storage</c>, both "open"-only)
/// and CommunityToolkit.Maui (whose <c>FileSaver</c> would otherwise be the natural choice) is
/// explicitly off-limits for this app (see CLAUDE.md/persona do agente <c>maui-frontend</c>) - so
/// the native Win32 dialog, reached the same way the project already reaches other
/// platform-specific native APIs (<c>Platforms/Windows/</c>), is the correct choice here.
/// </summary>
internal sealed class WindowsReportFileSaveDialog : IReportFileSaveDialog
{
    public Stream? RequestSaveStream(string suggestedFileName)
    {
        using var dialog = new SaveFileDialog
        {
            FileName = suggestedFileName,
            Filter = "Arquivo CSV (*.csv)|*.csv",
            DefaultExt = "csv",
            AddExtension = true,
        };

        var owner = new Win32Window(MainWindowManager.GetWindowHandle());
        DialogResult result = owner.Handle == IntPtr.Zero ? dialog.ShowDialog() : dialog.ShowDialog(owner);

        return result == DialogResult.OK ? dialog.OpenFile() : null;
    }

    /// <summary>Minimal <see cref="IWin32Window"/> wrapper around a raw HWND - <see cref="SaveFileDialog.ShowDialog(IWin32Window)"/> needs one to parent the dialog.</summary>
    private sealed class Win32Window : IWin32Window
    {
        public Win32Window(IntPtr handle)
        {
            Handle = handle;
        }

        public IntPtr Handle { get; }
    }
}
