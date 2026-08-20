using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace TaskEngine.Infrastructure.Monitoring;

/// <summary>
/// Real <see cref="IForegroundWindowSampler"/> using the Win32 <c>user32.dll</c> foreground-window
/// APIs (RF-002, ERS-Monitoramento.md; issue #11) - <c>GetForegroundWindow</c> to find the focused
/// window, <c>GetWindowText</c> for its title (used as an approximation of the visited page/tab per
/// the architecture decision recorded for this change), and <c>GetWindowThreadProcessId</c> +
/// <see cref="Process.GetProcessById(int)"/> to resolve the owning process name (used to detect
/// known browsers).
/// </summary>
public sealed class Win32ForegroundWindowSampler : IForegroundWindowSampler
{
    private const int MaxTitleLength = 512;

    public ForegroundWindowSample Sample()
    {
        var handle = GetForegroundWindow();
        if (handle == IntPtr.Zero)
        {
            return default;
        }

        var title = ReadWindowTitle(handle);

        _ = GetWindowThreadProcessId(handle, out var processId);
        var processName = ReadProcessName(processId);

        return new ForegroundWindowSample(processName, title);
    }

    private static string? ReadWindowTitle(IntPtr handle)
    {
        var buffer = new StringBuilder(MaxTitleLength);
        var length = GetWindowText(handle, buffer, buffer.Capacity);
        return length > 0 ? buffer.ToString(0, length) : null;
    }

    private static string? ReadProcessName(uint processId)
    {
        if (processId == 0)
        {
            return null;
        }

        try
        {
            using var process = Process.GetProcessById((int)processId);
            return process.ProcessName;
        }
        catch (ArgumentException)
        {
            // Process exited between the P/Invoke call and resolving it.
            return null;
        }
    }

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int count);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);
}
