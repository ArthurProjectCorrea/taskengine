using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.ApplicationModel;
using TaskEngine.Application.Abstractions;
using TaskEngine.Application.Providers;
using TaskEngine.Application.Reports;
using TaskEngine.Application.Tasks;
using TaskEngine.Application.WorkSessions;
using TaskEngine.Desktop.Navigation;
using TaskEngine.Desktop.Platforms.Windows;
using TaskEngine.Desktop.ViewModels;
using TaskEngine.Desktop.ViewModels.LocalBackup;
using TaskEngine.Desktop.ViewModels.Navigation;
using TaskEngine.Desktop.ViewModels.Providers;
using TaskEngine.Desktop.ViewModels.Reports;
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
        // no navegador; por isso não há client secret aqui (fluxo Authorization Code + PKCE) - o
        // GitHub exige client_secret na troca de código por token mesmo com PKCE, então essa etapa
        // (só ela) passa pela ponte em services/oauth-proxy/ (Cloudflare Worker), que guarda o
        // secret. Ver GitHubOAuthOptions.TokenExchangeProxyUrl para o racional completo.
        services.AddSingleton(new GitHubOAuthOptions(
            ClientId: "Ov23liY5GoHGRKLB1dfZ",
            Scopes: ["repo", "read:project"],
            TokenExchangeProxyUrl: "https://taskengine-oauth-proxy.arthurdepaulacorrea.workers.dev"));
        services.AddSingleton<IProviderAuthenticator, GitHubOAuthAuthenticator>();

        // IProviderClientFactory (issue #26) substitui o registro antigo de um único
        // ITaskProviderClient construído aqui no startup com opções placeholder (token fixo) -
        // essa fábrica resolve o client concreto sob demanda, injetando o token real (via
        // ICredentialStore, salvo pelo onboarding) só no momento do uso. Desde a troca para a
        // Issues Search API do GitHub (busca "is:issue assignee:@me"), não há mais owner/projeto
        // para escolher - a fábrica não precisa de nenhuma configuração além do token.
        services.AddSingleton<IProviderClientFactory, ProviderClientFactory>();

        // RF-007 (ERS-Tarefas.md, issue #18): was missing from this composition root entirely -
        // ConcludeTaskModalViewModel (registered below) takes ConcludeTaskUseCase as a constructor
        // dependency, so without this registration resolving it (and therefore every screen that
        // owns a conclude modal - Dashboard/Tarefas/DetalhesTarefa) throws at startup/navigation.
        services.AddTransient<ConcludeTaskUseCase>();

        // Dashboard (issue #19): work-session lifecycle use cases + the general task report use
        // case (GenerateTaskReportUseCase, RF-016), reused as-is by DashboardViewModel for the
        // selected task's human/AI/expediente/não-mapeado split - see its own doc comment.
        services.AddTransient<StartWorkSessionUseCase>();
        services.AddTransient<PauseWorkSessionUseCase>();
        services.AddTransient<EndWorkSessionUseCase>();
        services.AddTransient<GenerateTaskReportUseCase>();

        // Relatório por tarefa (plan item 9, RF-010/RF-011, ERS-Tarefas.md): the per-task
        // activity-timeline use case, plus the Windows-native "Salvar como" dialog port
        // (IReportFileSaveDialog) that RelatorioViewModel's CSV export uses - see
        // WindowsReportFileSaveDialog's own doc comment for why it's a native Win32 dialog and not
        // a third-party file-picker package.
        services.AddTransient<GenerateTaskActivityTimelineUseCase>();
        services.AddTransient<IReportFileSaveDialog, WindowsReportFileSaveDialog>();

        // Detalhes da Tarefa (RF-012/CA-012.1, ERS-Tarefas.md): opens the task's provider page in
        // the OS's default browser - see WindowsProviderLinkOpener's own doc comment.
        services.AddTransient<IProviderLinkOpener, WindowsProviderLinkOpener>();

        // Tarefas (RF-001, ERS-Tarefas.md): SyncTasksUseCase depends on EndWorkSessionUseCase too
        // (registered above) - not previously needed by the Dashboard.
        services.AddTransient<SyncTasksUseCase>();

        // Configurações (issue #52, ERS-Configuracoes.md): provider connect/disconnect use cases
        // (RF-001/RF-007) and local backup export/import (RF-003/RF-004, IBackupService) - neither
        // was registered anywhere before this screen needed them. IBackupFileDialog mirrors
        // IReportFileSaveDialog's Windows-native "Salvar como"/"Abrir" dialog approach (see
        // WindowsBackupFileDialog's own doc comment for why it returns paths, not streams).
        services.AddTransient<DisconnectProviderUseCase>();
        services.AddTransient<ReconnectProviderUseCase>();
        services.AddTransient<IBackupService, SqliteBackupService>();
        services.AddTransient<IBackupFileDialog, WindowsBackupFileDialog>();

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

        // "Concluir tarefa"/"Adicionar tempo não mapeado" modals (RF-006/RF-007, issue #18):
        // transient like every section view model below - Dashboard/Tarefas/DetalhesTarefa each
        // get their own fresh modal instance injected via constructor, so no state (open/closed,
        // checklist, form fields) leaks between screens or between navigations into the same one.
        services.AddTransient<ConcludeTaskModalViewModel>();
        services.AddTransient<AddUnmappedTimeModalViewModel>();

        // Dashboard (issue #19): transient, unlike the shell above - a fresh instance is resolved
        // by SectionViewFactory on every navigation into AppSection.Dashboard, not shared across
        // the app's lifetime like the single-window shell.
        services.AddTransient<DashboardViewModel>();
        services.AddTransient<DashboardPage>();

        // Tarefas (RF-001/RF-003, ERS-Tarefas.md): same transient lifetime as Dashboard above - a
        // fresh instance per navigation into AppSection.Tarefas.
        services.AddTransient<TarefasViewModel>();
        services.AddTransient<TarefasPage>();

        // Detalhes da Tarefa (issue #53, ERS-Tarefas.md): same transient lifetime as
        // Dashboard/Tarefas above - a fresh instance per navigation into
        // AppSection.DetalhesTarefa (see SectionViewFactory, which also feeds it the task id via
        // INavigationAware.ApplyParameter right after resolution).
        services.AddTransient<DetalhesTarefaViewModel>();
        services.AddTransient<DetalhesTarefaPage>();

        // Relatório (plan item 9, RF-010/RF-011, ERS-Tarefas.md): same transient lifetime as
        // Dashboard/Tarefas/DetalhesTarefa above - a fresh instance per navigation into
        // AppSection.Relatorio (also fed the task id via INavigationAware.ApplyParameter).
        services.AddTransient<RelatorioViewModel>();
        services.AddTransient<RelatorioPage>();

        // Configurações (issue #52, ERS-Configuracoes.md): same transient lifetime as every other
        // section above. ConfiguracoesViewModel is the one view model in this app that isn't built
        // purely from other DI-resolvable types - it also takes the app's own display version
        // (RF-006 "Sobre") as a plain string, read here via MAUI Essentials' AppInfo (this project
        // is not MAUI-free, unlike TaskEngine.Desktop.ViewModels - see that project's own .csproj
        // comment on why AppInfo cannot be called from inside the view model itself).
        services.AddTransient<ConfiguracoesViewModel>(sp => new ConfiguracoesViewModel(
            sp.GetRequiredService<IWorkScheduleStore>(),
            sp.GetRequiredService<IAppSettingsStore>(),
            sp.GetRequiredService<ICredentialStore>(),
            sp.GetRequiredService<IEnumerable<IProviderAuthenticator>>(),
            sp.GetRequiredService<DisconnectProviderUseCase>(),
            sp.GetRequiredService<ReconnectProviderUseCase>(),
            sp.GetRequiredService<IBackupService>(),
            sp.GetRequiredService<IBackupFileDialog>(),
            sp.GetRequiredService<GenerateTaskReportUseCase>(),
            sp.GetRequiredService<IReportFileSaveDialog>(),
            AppInfo.Current.VersionString));
        services.AddTransient<ConfiguracoesPage>();
    }
}
