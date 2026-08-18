using TaskEngine.Application.Providers;

namespace TaskEngine.Application.Abstractions;

/// <summary>
/// Port for logging a user into a task provider (e.g. GitHub, Jira, ClickUp) via that provider's
/// OAuth flow, obtaining an access token without the user having to create/paste one manually.
/// Implemented by TaskEngine.Infrastructure.
/// </summary>
public interface IProviderAuthenticator
{
    string ProviderId { get; }

    Task<ProviderAuthResult> AuthenticateAsync(CancellationToken cancellationToken);
}
