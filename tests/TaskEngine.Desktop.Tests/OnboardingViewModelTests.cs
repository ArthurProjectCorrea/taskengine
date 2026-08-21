using TaskEngine.Application.Providers;
using TaskEngine.Desktop.ViewModels;

namespace TaskEngine.Desktop.Tests;

public class OnboardingViewModelTests
{
    private static readonly ProviderAuthResult SuccessfulAuthResult =
        new("github", "gh-access-token", "repo read:project", DateTimeOffset.UtcNow);

    [Fact]
    public async Task ConnectAsync_OnSuccess_TransitionsToConnected_AndSavesToken()
    {
        var authenticator = new FakeProviderAuthenticator("github", SuccessfulAuthResult);
        var client = new FakeTaskProviderClient("github");
        var credentialStore = new FakeCredentialStore();
        var appSettingsStore = new FakeAppSettingsStore();

        var viewModel = new OnboardingViewModel([authenticator], credentialStore, appSettingsStore);

        await viewModel.ConnectAsync("github", CancellationToken.None);

        Assert.Equal(ConnectionState.Connected, viewModel.State);
        Assert.Equal("github", viewModel.ConnectedProviderId);
        Assert.Null(viewModel.ErrorMessage);

        Assert.Equal("gh-access-token", credentialStore.Secrets["provider:github:token"]);
        Assert.Equal("github", appSettingsStore.Values["provider:connected"]);
    }

    [Fact]
    public async Task ConnectAsync_WhenAuthenticatorThrows_TransitionsToError_AndDoesNotPersistAnything()
    {
        var authenticator = new FakeProviderAuthenticator("github", new InvalidOperationException("GitHub login was cancelled or denied by the user."));
        var credentialStore = new FakeCredentialStore();
        var appSettingsStore = new FakeAppSettingsStore();

        var viewModel = new OnboardingViewModel([authenticator], credentialStore, appSettingsStore);

        await viewModel.ConnectAsync("github", CancellationToken.None);

        Assert.Equal(ConnectionState.Error, viewModel.State);
        Assert.Equal("GitHub login was cancelled or denied by the user.", viewModel.ErrorMessage);
        Assert.Null(viewModel.ConnectedProviderId);

        Assert.Empty(credentialStore.Secrets);
        Assert.Empty(appSettingsStore.Values);
    }

    [Fact]
    public async Task GetAlreadyConnectedProviderIdAsync_WhenNoProviderConnected_ReturnsNull()
    {
        var authenticator = new FakeProviderAuthenticator("github", SuccessfulAuthResult);
        var appSettingsStore = new FakeAppSettingsStore();

        var viewModel = new OnboardingViewModel([authenticator], new FakeCredentialStore(), appSettingsStore);

        var connectedProviderId = await viewModel.GetAlreadyConnectedProviderIdAsync(CancellationToken.None);

        Assert.Null(connectedProviderId);
    }

    [Fact]
    public async Task GetAlreadyConnectedProviderIdAsync_WhenAProviderWasPreviouslyConnected_ReturnsItsId()
    {
        var authenticator = new FakeProviderAuthenticator("github", SuccessfulAuthResult);
        var appSettingsStore = new FakeAppSettingsStore();
        await appSettingsStore.SetAsync("provider:connected", "github", CancellationToken.None);

        var viewModel = new OnboardingViewModel([authenticator], new FakeCredentialStore(), appSettingsStore);

        var connectedProviderId = await viewModel.GetAlreadyConnectedProviderIdAsync(CancellationToken.None);

        Assert.Equal("github", connectedProviderId);
    }

    [Fact]
    public void Providers_IsPopulatedFromRegisteredAuthenticators_WithKnownDisplayName()
    {
        var authenticator = new FakeProviderAuthenticator("github", SuccessfulAuthResult);

        var viewModel = new OnboardingViewModel([authenticator], new FakeCredentialStore(), new FakeAppSettingsStore());

        var provider = Assert.Single(viewModel.Providers);
        Assert.Equal("github", provider.ProviderId);
        Assert.Equal("GitHub", provider.DisplayName);
    }
}
