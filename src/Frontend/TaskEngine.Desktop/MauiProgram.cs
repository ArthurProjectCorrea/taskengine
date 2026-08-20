using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TaskEngine.Application.Abstractions;
using TaskEngine.Application.Reports;
using TaskEngine.Application.Tasks;
using TaskEngine.Application.WorkSessions;
using TaskEngine.Desktop.Navigation;
using TaskEngine.Desktop.ViewModels;
using TaskEngine.Desktop.ViewModels.Navigation;
using TaskEngine.Desktop.Views;
using TaskEngine.Infrastructure.Monitoring;
using TaskEngine.Infrastructure.Persistence;
using TaskEngine.Infrastructure.Providers;
using TaskEngine.Infrastructure.Providers.GitHub;

namespace TaskEngine.Desktop;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

#if DEBUG
        builder.Logging.AddDebug();
#endif

        RegisterBackendServices(builder.Services);
        RegisterPresentation(builder.Services);

        var app = builder.Build();

        // Auto-provisionamento do schema SQLite (issue #16) no arranque do app. Executado de forma
        // síncrona e bloqueante aqui (não fire-and-forget): é um custo único e rápido (CREATE TABLE
        // IF NOT EXISTS locais), e o restante do composition root (ex.: decidir entre OnboardingPage
        // e MainPage em App.CreateWindow, que já lê/grava em app_settings) depende do schema já
        // existir. Fire-and-forget introduziria uma corrida real contra esse startup; um pacote de
        // .NET Generic Host completo para orquestrar isso corretamente seria over-engineering para
        // um app desktop de usuário único.
        app.Services.GetRequiredService<SqliteDatabaseInitializer>()
            .EnsureCreatedAsync()
            .GetAwaiter()
            .GetResult();

        // Monitoring module (ERS-Monitoramento.md, RN-001): runs continuously for the whole app
        // lifetime, independent of any task being in progress - started once here, never gated by
        // task/session state.
        app.Services.GetRequiredService<IFileActivityWatcher>().Start();
        app.Services.GetRequiredService<IWindowFocusWatcher>().Start();

        return app;
    }

    private static void RegisterBackendServices(IServiceCollection services)
    {
        services.AddSingleton<HttpClient>();

        services.AddSingleton<SqlitePathProvider>();
        services.AddSingleton<SqliteDatabaseInitializer>();
        services.AddSingleton<IAppSettingsStore, SqliteAppSettingsStore>();
        services.AddSingleton<ICredentialStore, DpapiCredentialStore>();
        services.AddSingleton<ITaskRepository, SqliteTaskRepository>();
        services.AddSingleton<IWorkSessionRepository, SqliteWorkSessionRepository>();
        services.AddSingleton<IWorkScheduleStore, AppSettingsWorkScheduleStore>();
        services.AddSingleton<IUnmappedTimeEntryRepository, SqliteUnmappedTimeEntryRepository>();

        // Client ID do GitHub App real registrado pelo Arthur (ver issue #22). Não é segredo -
        // client_id de um cliente público PKCE vai literalmente na URL de autorização, visível
        // no navegador; por isso não há client secret aqui (fluxo Authorization Code + PKCE).
        services.AddSingleton(new GitHubOAuthOptions(
            ClientId: "Ov23liY5GoHGRKLB1dfZ",
            Scopes: ["repo", "read:project"]));
        services.AddSingleton<IProviderAuthenticator, GitHubOAuthAuthenticator>();

        // IProviderClientFactory (issue #26) substitui o registro antigo de um único
        // ITaskProviderClient construído aqui no startup com opções placeholder (token/OwnerLogin/
        // ProjectNumber fixos) - essa fábrica resolve o client concreto sob demanda, injetando o
        // token real (via ICredentialStore, salvo pelo onboarding) só no momento do uso.
        // OwnerLogin/ProjectNumber continuam placeholder dentro da própria fábrica (ver
        // TaskEngine.Infrastructure.Providers.ProviderClientFactory) até existir uma tela de
        // escolha de projeto do GitHub - fora do escopo da #26.
        services.AddSingleton<IProviderClientFactory, ProviderClientFactory>();

        services.AddTransient<CreateTaskUseCase>();

        // Dashboard (issue #19): work-session lifecycle use cases + the general task report use
        // case (GenerateTaskReportUseCase, RF-016), reused as-is by DashboardViewModel for the
        // selected task's human/AI/expediente/não-mapeado split - see its own doc comment.
        services.AddTransient<StartWorkSessionUseCase>();
        services.AddTransient<PauseWorkSessionUseCase>();
        services.AddTransient<GenerateTaskReportUseCase>();

        // Monitoring module (RF-001/RF-002, ERS-Monitoramento.md, issues #12/#11). Registered as
        // singletons so the same watcher instance (and its in-memory debounce/aggregation state)
        // lives for the whole app lifetime - see Start() call in CreateMauiApp above.
        services.AddSingleton<IMonitoredActivityRepository, SqliteMonitoredActivityRepository>();
        services.AddSingleton<IRunningProcessesProvider, SystemRunningProcessesProvider>();
        services.AddSingleton(FileActivityWatcherOptions.CreateDefault());
        services.AddSingleton<IFileActivityWatcher, FileSystemActivityWatcher>();
        services.AddSingleton<IForegroundWindowSampler, Win32ForegroundWindowSampler>();
        services.AddSingleton(new WindowFocusWatcherOptions());
        services.AddSingleton<IWindowFocusWatcher, WindowFocusActivityWatcher>();
    }

    private static void RegisterPresentation(IServiceCollection services)
    {
        services.AddTransient<OnboardingViewModel>();
        services.AddTransient<OnboardingPage>();

        // Shell/navegação (issue #2). Singletons: há uma única janela para o app inteiro - ela
        // nunca é recriada, só escondida/mostrada (ver MainWindowManager) - então o estado de
        // navegação (qual seção está ativa) precisa sobreviver a esses ciclos, não ser recriado a
        // cada resolução do container.
        services.AddSingleton<INavigationService, NavigationService>();
        services.AddSingleton<SectionViewFactory>();
        services.AddSingleton<ShellViewModel>();
        services.AddSingleton<MainShellPage>();

        // Dashboard (issue #19): transient, unlike the shell above - a fresh instance is resolved
        // by SectionViewFactory on every navigation into AppSection.Dashboard (same lifetime
        // PlaceholderView already gets for the other sections), not shared across the app's
        // lifetime like the single-window shell.
        services.AddTransient<DashboardViewModel>();
        services.AddTransient<DashboardPage>();
    }
}
