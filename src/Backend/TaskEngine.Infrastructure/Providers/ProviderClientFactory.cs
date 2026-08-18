using TaskEngine.Application.Abstractions;
using TaskEngine.Infrastructure.Providers.GitHub;

namespace TaskEngine.Infrastructure.Providers;

/// <summary>
/// <see cref="IProviderClientFactory"/> implementation. Resolves the provider's real access token
/// via <see cref="ICredentialStore"/> at the moment a client is requested, rather than
/// constructing a client early with a placeholder token (the mistake made - and corrected for
/// schema fetching - in issue #20).
/// </summary>
public sealed class ProviderClientFactory : IProviderClientFactory
{
    private readonly HttpClient _httpClient;
    private readonly ICredentialStore _credentialStore;

    public ProviderClientFactory(HttpClient httpClient, ICredentialStore credentialStore)
    {
        _httpClient = httpClient;
        _credentialStore = credentialStore;
    }

    public async Task<ITaskProviderClient> CreateAsync(string providerId, CancellationToken cancellationToken)
    {
        var token = await _credentialStore.GetAsync($"provider:{providerId}:token", cancellationToken);
        if (token is null)
        {
            throw new InvalidOperationException($"Provider '{providerId}' is not connected.");
        }

        if (providerId == "github")
        {
            // TODO: Owner login and project number are placeholders - there is no UI yet for the
            // user to pick which GitHub Projects v2 board a task should be created in (tracked
            // separately; out of scope for issue #26, which only wires up the "create on
            // provider" flow itself).
            return new GitHubProjectsClient(
                _httpClient,
                new GitHubProjectsOptions(token, OwnerLogin: "TODO_GITHUB_PROJECTS_OWNER_LOGIN", ProjectNumber: 0));
        }

        throw new InvalidOperationException($"Unknown provider '{providerId}'.");
    }
}
