using TaskEngine.Application.Abstractions;
using TaskEngine.Application.Providers;
using TaskEngine.Desktop.ViewModels;

namespace TaskEngine.Desktop.Tests;

public class OnboardingViewModelTests
{
    private static readonly ProviderAuthResult SuccessfulAuthResult =
        new("github", "gh-access-token", "repo read:project", DateTimeOffset.UtcNow);

    private static readonly ProviderTaskSchema SampleSchema =
        new("github", [new ProviderFieldDefinition("status", "Status", ProviderFieldType.SingleSelect, false, [])]);

    [Fact]
    public async Task ConnectAsync_OnSuccess_TransitionsToConnected_AndSavesTokenAndSchema()
    {
        var authenticator = new FakeProviderAuthenticator("github", SuccessfulAuthResult);
        var client = new FakeTaskProviderClient("github", SampleSchema);
        var credentialStore = new FakeCredentialStore();
        var appSettingsStore = new FakeAppSettingsStore();

        var viewModel = new OnboardingViewModel([authenticator], new FakeProviderClientFactory(client), credentialStore, appSettingsStore);

        await viewModel.ConnectAsync("github", CancellationToken.None);

        Assert.Equal(ConnectionState.Connected, viewModel.State);
        Assert.Equal("github", viewModel.ConnectedProviderId);
        Assert.Null(viewModel.ErrorMessage);

        Assert.Equal("gh-access-token", credentialStore.Secrets["provider:github:token"]);
        Assert.True(appSettingsStore.Values.ContainsKey("provider:github:schema"));
        Assert.Contains("status", appSettingsStore.Values["provider:github:schema"]);
        Assert.Equal("github", appSettingsStore.Values["provider:connected"]);
    }

    [Fact]
    public async Task ConnectAsync_WhenAuthenticatorThrows_TransitionsToError_AndDoesNotPersistAnything()
    {
        var authenticator = new FakeProviderAuthenticator("github", new InvalidOperationException("GitHub login was cancelled or denied by the user."));
        var client = new FakeTaskProviderClient("github", SampleSchema);
        var credentialStore = new FakeCredentialStore();
        var appSettingsStore = new FakeAppSettingsStore();

        var viewModel = new OnboardingViewModel([authenticator], new FakeProviderClientFactory(client), credentialStore, appSettingsStore);

        await viewModel.ConnectAsync("github", CancellationToken.None);

        Assert.Equal(ConnectionState.Error, viewModel.State);
        Assert.Equal("GitHub login was cancelled or denied by the user.", viewModel.ErrorMessage);
        Assert.Null(viewModel.ConnectedProviderId);

        Assert.Empty(credentialStore.Secrets);
        Assert.Empty(appSettingsStore.Values);
        Assert.Equal(0, client.GetTaskSchemaCallCount);
    }

    [Fact]
    public async Task ConnectAsync_WhenSchemaFetchThrows_StillTransitionsToConnected_SchemaFetchIsBestEffort()
    {
        var authenticator = new FakeProviderAuthenticator("github", SuccessfulAuthResult);
        var client = new FakeTaskProviderClient("github", new InvalidOperationException("GitHub project not found."));
        var credentialStore = new FakeCredentialStore();
        var appSettingsStore = new FakeAppSettingsStore();

        var viewModel = new OnboardingViewModel([authenticator], new FakeProviderClientFactory(client), credentialStore, appSettingsStore);

        await viewModel.ConnectAsync("github", CancellationToken.None);

        // A login that succeeded must not be reported as an error just because the (best-effort)
        // schema pre-fetch failed - and "connected" must still be flagged, otherwise the
        // onboarding screen would loop forever on the next start despite a valid saved token.
        Assert.Equal(ConnectionState.Connected, viewModel.State);
        Assert.Null(viewModel.ErrorMessage);
        Assert.Equal("github", viewModel.ConnectedProviderId);

        Assert.Equal("gh-access-token", credentialStore.Secrets["provider:github:token"]);
        Assert.Equal("github", appSettingsStore.Values["provider:connected"]);
        Assert.False(appSettingsStore.Values.ContainsKey("provider:github:schema"));
    }

    [Fact]
    public async Task GetAlreadyConnectedProviderIdAsync_WhenNoProviderConnected_ReturnsNull()
    {
        var authenticator = new FakeProviderAuthenticator("github", SuccessfulAuthResult);
        var client = new FakeTaskProviderClient("github", SampleSchema);
        var appSettingsStore = new FakeAppSettingsStore();

        var viewModel = new OnboardingViewModel([authenticator], new FakeProviderClientFactory(client), new FakeCredentialStore(), appSettingsStore);

        var connectedProviderId = await viewModel.GetAlreadyConnectedProviderIdAsync(CancellationToken.None);

        Assert.Null(connectedProviderId);
    }

    [Fact]
    public async Task GetAlreadyConnectedProviderIdAsync_WhenAProviderWasPreviouslyConnected_ReturnsItsId()
    {
        var authenticator = new FakeProviderAuthenticator("github", SuccessfulAuthResult);
        var client = new FakeTaskProviderClient("github", SampleSchema);
        var appSettingsStore = new FakeAppSettingsStore();
        await appSettingsStore.SetAsync("provider:connected", "github", CancellationToken.None);

        var viewModel = new OnboardingViewModel([authenticator], new FakeProviderClientFactory(client), new FakeCredentialStore(), appSettingsStore);

        var connectedProviderId = await viewModel.GetAlreadyConnectedProviderIdAsync(CancellationToken.None);

        Assert.Equal("github", connectedProviderId);
    }

    [Fact]
    public void Providers_IsPopulatedFromRegisteredAuthenticators_WithKnownDisplayName()
    {
        var authenticator = new FakeProviderAuthenticator("github", SuccessfulAuthResult);
        var client = new FakeTaskProviderClient("github", SampleSchema);

        var viewModel = new OnboardingViewModel([authenticator], new FakeProviderClientFactory(client), new FakeCredentialStore(), new FakeAppSettingsStore());

        var provider = Assert.Single(viewModel.Providers);
        Assert.Equal("github", provider.ProviderId);
        Assert.Equal("GitHub", provider.DisplayName);
    }
}
