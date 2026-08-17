namespace TaskEngine.Desktop;

/// <summary>
/// Implementação Windows dos hooks de ciclo de vida declarados em <c>App.xaml.cs</c> (compartilhado):
/// aqui, e só aqui, vivem os tipos específicos de plataforma (AppWindow, RegisterHotKey, NotifyIcon,
/// registro do Windows) que dão à janela do MAUI o comportamento de barra flutuante estilo
/// Raycast/Spotlight residente na bandeja.
/// </summary>
public partial class App
{
    private Platforms.Windows.GlobalHotKey? _hotKey;
    private Platforms.Windows.TrayIconService? _trayIcon;

    partial void OnWindowCreated(Window window)
    {
        Platforms.Windows.FloatingWindowManager.Initialize(window);
        Platforms.Windows.AutoStartService.EnsureRegistered();

        _trayIcon = new Platforms.Windows.TrayIconService();
        _trayIcon.OpenRequested += Platforms.Windows.FloatingWindowManager.Toggle;
        _trayIcon.ExitRequested += ExitApplication;

        _hotKey = new Platforms.Windows.GlobalHotKey();
        _hotKey.Pressed += Platforms.Windows.FloatingWindowManager.Toggle;
    }

    partial void OnWindowDeactivated(Window window) => Platforms.Windows.FloatingWindowManager.Hide();

    /// <summary>Encerra o processo de verdade (diferente de apenas esconder a janela).</summary>
    private void ExitApplication()
    {
        _hotKey?.Dispose();
        _trayIcon?.Dispose();

        Environment.Exit(0);
    }
}
