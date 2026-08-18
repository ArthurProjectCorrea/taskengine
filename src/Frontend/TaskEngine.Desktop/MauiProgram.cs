using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TaskEngine.Application.Abstractions;
using TaskEngine.Desktop.ViewModels;
using TaskEngine.Desktop.Views;
using TaskEngine.Infrastructure.Persistence;
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

        return app;
    }

    private static void RegisterBackendServices(IServiceCollection services)
    {
        services.AddSingleton<HttpClient>();

        services.AddSingleton<SqlitePathProvider>();
        services.AddSingleton<SqliteDatabaseInitializer>();
        services.AddSingleton<IAppSettingsStore, SqliteAppSettingsStore>();
        services.AddSingleton<ICredentialStore, DpapiCredentialStore>();

        // TODO: ClientId é um placeholder até o Arthur registrar o GitHub App real (ver issue #22).
        // Sem client secret de propósito: fluxo Authorization Code + PKCE para client público.
        services.AddSingleton(new GitHubOAuthOptions(
            ClientId: "TODO_GITHUB_APP_CLIENT_ID",
            Scopes: ["repo", "read:project"]));
        services.AddSingleton<IProviderAuthenticator, GitHubOAuthAuthenticator>();

        // TODO: AccessToken/OwnerLogin/ProjectNumber são placeholders. Na prática só fazem sentido
        // depois que o usuário conecta (AccessToken vem do OAuth) e escolhe o projeto de destino no
        // GitHub - isso ainda não tem UI própria. Placeholder aqui só permite que GitHubProjectsClient
        // seja resolvido pelo DI; GetTaskSchemaAsync vai falhar contra a API real até isso ser
        // preenchido de verdade, o que é aceitável nesta fase (onboarding só lista provedores e
        // dispara auth - a chamada real ao client já pressupõe token/projeto configurados).
        services.AddSingleton(new GitHubProjectsOptions(
            AccessToken: "TODO_GITHUB_PROJECTS_ACCESS_TOKEN",
            OwnerLogin: "TODO_GITHUB_PROJECTS_OWNER_LOGIN",
            ProjectNumber: 0));
        services.AddSingleton<ITaskProviderClient, GitHubProjectsClient>();
    }

    private static void RegisterPresentation(IServiceCollection services)
    {
        services.AddTransient<OnboardingViewModel>();
        services.AddTransient<OnboardingPage>();
    }
}
