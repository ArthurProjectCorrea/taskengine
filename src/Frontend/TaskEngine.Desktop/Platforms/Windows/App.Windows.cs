using System.Linq;

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

        // O instalador (releases/v1/TaskEngine.Setup.iss, seção [Run]) passa "/show" quando o
        // usuário marca "Abrir o TaskEngine agora" ao final da instalação. Sem isso, Initialize()
        // acima esconde a janela (comportamento correto para abertura normal/autostart) e o app
        // apenas pisca na tela e some para a bandeja, dando a impressão de que travou.
        if (WasLaunchedWithShowArgument())
        {
            Platforms.Windows.MainWindowManager.Show();
        }
    }

    /// <summary>
    /// Verifica se o processo foi iniciado com o argumento "/show" (ou "-show"), usado pelo
    /// instalador para pedir que a janela apareça imediatamente em vez de iniciar escondida na
    /// bandeja. <see cref="Environment.GetCommandLineArgs"/> funciona em qualquer app .NET; o
    /// primeiro elemento é o caminho do executável, por isso é ignorado na comparação.
    /// </summary>
    private static bool WasLaunchedWithShowArgument() =>
        Environment.GetCommandLineArgs()
            .Skip(1)
            .Any(arg => string.Equals(arg, "/show", StringComparison.OrdinalIgnoreCase)
                || string.Equals(arg, "-show", StringComparison.OrdinalIgnoreCase));

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
