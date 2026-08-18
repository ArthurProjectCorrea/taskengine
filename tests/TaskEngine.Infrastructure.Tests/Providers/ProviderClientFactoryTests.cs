using TaskEngine.Application.Abstractions;
using TaskEngine.Infrastructure.Providers;
using TaskEngine.Infrastructure.Providers.GitHub;

namespace TaskEngine.Infrastructure.Tests.Providers;

public class ProviderClientFactoryTests
{
    [Fact]
    public async Task CreateAsync_WhenNoTokenIsStored_ThrowsWithClearMessage()
    {
        var credentialStore = new FakeCredentialStore();
        var factory = new ProviderClientFactory(new HttpClient(), credentialStore);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => factory.CreateAsync("github", CancellationToken.None));

        Assert.Equal("Provider 'github' is not connected.", exception.Message);
    }

    [Fact]
    public async Task CreateAsync_WhenTokenIsStoredForGitHub_ReturnsGitHubProjectsClient()
    {
        var credentialStore = new FakeCredentialStore();
        await credentialStore.SaveAsync("provider:github:token", "gh-token", CancellationToken.None);
        var factory = new ProviderClientFactory(new HttpClient(), credentialStore);

        var client = await factory.CreateAsync("github", CancellationToken.None);

        Assert.IsType<GitHubProjectsClient>(client);
        Assert.Equal("github", client.ProviderId);
    }

    [Fact]
    public async Task CreateAsync_WhenProviderIsUnknown_Throws()
    {
        var credentialStore = new FakeCredentialStore();
        await credentialStore.SaveAsync("provider:unknown:token", "some-token", CancellationToken.None);
        var factory = new ProviderClientFactory(new HttpClient(), credentialStore);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => factory.CreateAsync("unknown", CancellationToken.None));

        Assert.Equal("Unknown provider 'unknown'.", exception.Message);
    }

    /// <summary>Hand-written in-memory fake for <see cref="ICredentialStore"/>, no mocking library.</summary>
    private sealed class FakeCredentialStore : ICredentialStore
    {
        private readonly Dictionary<string, string> _secrets = [];

        public Task SaveAsync(string key, string secret, CancellationToken cancellationToken)
        {
            _secrets[key] = secret;
            return Task.CompletedTask;
        }

        public Task<string?> GetAsync(string key, CancellationToken cancellationToken)
        {
            return Task.FromResult(_secrets.GetValueOrDefault(key));
        }

        public Task DeleteAsync(string key, CancellationToken cancellationToken)
        {
            _secrets.Remove(key);
            return Task.CompletedTask;
        }
    }
}
