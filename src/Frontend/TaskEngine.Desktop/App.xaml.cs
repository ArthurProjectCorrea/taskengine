using Microsoft.Extensions.DependencyInjection;
using TaskEngine.Desktop.ViewModels;
using TaskEngine.Desktop.Views;

namespace TaskEngine.Desktop;

/// <summary>
/// Classe compartilhada e multiplataforma. Nenhum tipo específico de SO é referenciado aqui:
/// os hooks de ciclo de vida abaixo são implementados via partial methods em
/// <c>Platforms/&lt;Plataforma&gt;/App.&lt;Plataforma&gt;.cs</c> (ex.: <c>Platforms/Windows/App.Windows.cs</c>).
/// </summary>
/// <remarks>
/// Base class qualificada como <c>Microsoft.Maui.Controls.Application</c> (em vez do simples
/// <c>Application</c>) porque, a partir da issue #20, este projeto referencia (transitivamente)
/// <c>TaskEngine.Application</c> — como esse é um namespace irmão de <c>TaskEngine.Desktop</c> sob
/// a raiz comum <c>TaskEngine</c>, ele tem prioridade de resolução sobre o tipo importado via
/// <c>using</c> global do MAUI, e "Application" sem qualificação passa a apontar para o
/// namespace, não para o tipo.
/// </remarks>
public partial class App : Microsoft.Maui.Controls.Application
{
    private readonly IServiceProvider _serviceProvider;

    /// <summary>
    /// <paramref name="serviceProvider"/> é injetado automaticamente pelo container de DI do MAUI
    /// (o próprio <c>App</c> é resolvido via DI, conforme registrado por <c>UseMauiApp&lt;App&gt;()</c>
    /// em <c>MauiProgram.cs</c>). Usado aqui só para decidir/criar a página inicial (issue #20) -
    /// evita espalhar `IServiceProvider` por mais lugares do que o necessário.
    /// </summary>
    public App(IServiceProvider serviceProvider)
    {
        InitializeComponent();
        _serviceProvider = serviceProvider;
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        var window = new Window(ResolveStartupPage());

        window.Created += (_, _) => OnWindowCreated(window);
        window.Deactivated += (_, _) => OnWindowDeactivated(window);

        return window;
    }

    /// <summary>
    /// Decisão de startup (issue #20, ajustada minimamente na #26 - navegação completa
    /// continua fora de escopo, ver #2): se já existe um provedor conectado (checado via
    /// <see cref="OnboardingViewModel.GetAlreadyConnectedProviderIdAsync"/>), pula direto para
    /// <see cref="CreateTaskPage"/> (resolvida via DI, mesmo padrão usado abaixo para
    /// <see cref="OnboardingPage"/> - nenhum sistema de navegação novo foi introduzido, só a
    /// página inicial trocou de "AppShell/MainPage" placeholder para a tela real que a #26
    /// implementa); caso contrário, mostra <see cref="OnboardingPage"/> primeiro. Depois que o
    /// onboarding conecta um provedor com sucesso, a navegação para <see cref="CreateTaskPage"/>
    /// só acontece no próximo start - navegar automaticamente na hora exigiria um sistema de
    /// navegação que é escopo da #2.
    /// </summary>
    private Page ResolveStartupPage()
    {
        var onboardingViewModel = _serviceProvider.GetRequiredService<OnboardingViewModel>();
        var connectedProviderId = onboardingViewModel
            .GetAlreadyConnectedProviderIdAsync(CancellationToken.None)
            .GetAwaiter()
            .GetResult();

        if (connectedProviderId is not null)
        {
            return _serviceProvider.GetRequiredService<CreateTaskPage>();
        }

        return _serviceProvider.GetRequiredService<OnboardingPage>();
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