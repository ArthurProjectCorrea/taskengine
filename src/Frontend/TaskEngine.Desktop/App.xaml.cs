namespace TaskEngine.Desktop;

/// <summary>
/// Classe compartilhada e multiplataforma. Nenhum tipo específico de SO é referenciado aqui:
/// os hooks de ciclo de vida abaixo são implementados via partial methods em
/// <c>Platforms/&lt;Plataforma&gt;/App.&lt;Plataforma&gt;.cs</c> (ex.: <c>Platforms/Windows/App.Windows.cs</c>).
/// </summary>
public partial class App : Application
{
    public App()
    {
        InitializeComponent();
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        var window = new Window(new AppShell());

        window.Created += (_, _) => OnWindowCreated(window);
        window.Deactivated += (_, _) => OnWindowDeactivated(window);

        return window;
    }

    /// <summary>
    /// Disparado quando a janela nativa da plataforma foi criada. É aqui que a implementação
    /// de cada plataforma configura o comportamento de janela flutuante (frameless, sempre no
    /// topo, fora da barra de tarefas), registra o atalho global, o ícone de bandeja e o
    /// início automático com o SO — e esconde a janela para o app iniciar residente na bandeja.
    /// </summary>
    partial void OnWindowCreated(Window window);

    /// <summary>
    /// Disparado quando a janela perde o foco (ex.: usuário clica fora). Cada plataforma decide
    /// como esconder a janela sem encerrar o processo.
    /// </summary>
    partial void OnWindowDeactivated(Window window);
}