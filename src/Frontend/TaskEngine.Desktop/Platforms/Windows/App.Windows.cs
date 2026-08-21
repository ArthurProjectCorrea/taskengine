namespace TaskEngine.Desktop;

/// <summary>
/// Implementação Windows dos hooks de ciclo de vida declarados em <c>App.xaml.cs</c> (compartilhado):
/// aqui, e só aqui, vivem os tipos específicos de plataforma (AppWindow, RegisterHotKey, NotifyIcon,
/// registro do Windows) que configuram a janela principal (moldura, tamanho, comportamento de
/// bandeja) e o atalho global que a mostra/esconde.
/// </summary>
public partial class App
{
    private Platforms.Windows.GlobalHotKey? _hotKey;
    private Platforms.Windows.TrayIconService? _trayIcon;

    partial void OnWindowCreated(Window window)
    {
        Platforms.Windows.MainWindowManager.Initialize(window);
        Platforms.Windows.AutoStartService.EnsureRegistered();

        _trayIcon = new Platforms.Windows.TrayIconService();
        _trayIcon.OpenRequested += Platforms.Windows.MainWindowManager.Toggle;
        _trayIcon.ExitRequested += ExitApplication;

        _hotKey = new Platforms.Windows.GlobalHotKey();
        _hotKey.Pressed += Platforms.Windows.MainWindowManager.Toggle;
    }

    // OnWindowDeactivated (declarado como partial void opcional em App.xaml.cs) é deliberadamente
    // não implementado aqui: antes chamava MainWindowManager.Hide() ao perder o foco, sobra do
    // modelo antigo de barra de comando flutuante estilo Raycast (onde clicar fora escondia a
    // barra). Agora a janela é normal, redimensionável, com sidebar (ver MainWindowManager) -
    // perder o foco não deve esconder nem fechar nada, só minimizar manual (pela usuária) ou
    // fechar pelo X (hide-to-tray, ver OnAppWindowClosing) afetam a visibilidade. Sem
    // implementação, o partial void vira no-op automaticamente.

    /// <summary>Encerra o processo de verdade (diferente de apenas esconder a janela).</summary>
    private void ExitApplication()
    {
        _hotKey?.Dispose();
        _trayIcon?.Dispose();

        Environment.Exit(0);
    }
}
