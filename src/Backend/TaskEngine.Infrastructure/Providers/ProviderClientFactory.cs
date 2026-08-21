using TaskEngine.Application.Abstractions;
using TaskEngine.Application.Providers;
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
    private readonly IAppSettingsStore _appSettingsStore;

    public ProviderClientFactory(HttpClient httpClient, ICredentialStore credentialStore, IAppSettingsStore appSettingsStore)
    {
        _httpClient = httpClient;
        _credentialStore = credentialStore;
        _appSettingsStore = appSettingsStore;
    }

    public async Task<ITaskProviderClient> CreateAsync(string providerId, CancellationToken cancellationToken)
    {
        var token = await _credentialStore.GetAsync(ProviderSettingsKeys.CredentialKey(providerId), cancellationToken);
        if (token is null)
        {
            throw new InvalidOperationException($"Provider '{providerId}' is not connected.");
        }

        if (providerId == "github")
        {
            return new GitHubIssuesClient(_httpClient, new GitHubIssuesOptions(token), _appSettingsStore);
        }

        throw new InvalidOperationException($"Unknown provider '{providerId}'.");
    }
}
