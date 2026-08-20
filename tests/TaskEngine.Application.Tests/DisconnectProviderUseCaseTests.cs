using TaskEngine.Application.Providers;

namespace TaskEngine.Application.Tests;

public class DisconnectProviderUseCaseTests
{
    [Fact]
    public async Task ExecuteAsync_FreezesTheProvider()
    {
        var credentialStore = new FakeCredentialStore();
        var appSettingsStore = new FakeAppSettingsStore();
        var useCase = new DisconnectProviderUseCase(credentialStore, appSettingsStore);

        await useCase.ExecuteAsync("github");

        Assert.Equal("true", appSettingsStore.Values[ProviderSettingsKeys.Frozen("github")]);
    }

    [Fact]
    public async Task ExecuteAsync_DeletesTheStoredAccessToken()
    {
        var credentialStore = new FakeCredentialStore();
        await credentialStore.SaveAsync(ProviderSettingsKeys.CredentialKey("github"), "gh-token", CancellationToken.None);
        var appSettingsStore = new FakeAppSettingsStore();
        var useCase = new DisconnectProviderUseCase(credentialStore, appSettingsStore);

        await useCase.ExecuteAsync("github");

        Assert.DoesNotContain(ProviderSettingsKeys.CredentialKey("github"), credentialStore.Secrets.Keys);
    }
}
