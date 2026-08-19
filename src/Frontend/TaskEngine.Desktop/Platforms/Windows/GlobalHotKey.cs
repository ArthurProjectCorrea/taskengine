using System.Runtime.InteropServices;
using Message = System.Windows.Forms.Message;
using NativeWindow = System.Windows.Forms.NativeWindow;

namespace TaskEngine.Desktop.Platforms.Windows;

/// <summary>
/// Registra o atalho global Alt+Espaço via <c>RegisterHotKey</c> (Win32/user32.dll), recebido
/// através de uma <see cref="NativeWindow"/> invisível criada só para receber <c>WM_HOTKEY</c>.
/// Isolado em Platforms/Windows — nenhum P/Invoke vaza para código compartilhado.
/// </summary>
internal sealed class GlobalHotKey : NativeWindow, IDisposable
{
    private const int WM_HOTKEY = 0x0312;
    private const int HotKeyId = 1;

    private const uint MOD_ALT = 0x0001;
    private const uint MOD_NOREPEAT = 0x4000;
    private const uint VK_SPACE = 0x20;

    private bool _registered;

    /// <summary>
    /// Disparado na thread de UI sempre que Alt+Espaço é pressionado. Não sabe nada sobre janela
    /// ou geometria - quem decide o que "toggle" significa é o assinante (hoje,
    /// <c>MainWindowManager.Toggle</c>, ligado em <c>App.Windows.cs</c>): mostrar/esconder a
    /// janela principal, mantendo hide-to-tray.
    /// </summary>
    public event Action? Pressed;

    public GlobalHotKey()
    {
        CreateHandle(new System.Windows.Forms.CreateParams());

        _registered = RegisterHotKey(Handle, HotKeyId, MOD_ALT | MOD_NOREPEAT, VK_SPACE);
        if (!_registered)
        {
            throw new InvalidOperationException("Não foi possível registrar o atalho global Alt+Espaço.");
        }
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WM_HOTKEY && m.WParam.ToInt32() == HotKeyId)
        {
            Pressed?.Invoke();
        }

        base.WndProc(ref m);
    }

    public void Dispose()
    {
        if (_registered)
        {
            UnregisterHotKey(Handle, HotKeyId);
            _registered = false;
        }

        DestroyHandle();
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
}
