using System.Collections.ObjectModel;
using System.Text.Json;
using TaskEngine.Application.Abstractions;
using TaskEngine.Desktop.Mvvm;

namespace TaskEngine.Desktop.ViewModels;

/// <summary>
/// One provider entry rendered by <c>OnboardingPage</c> — pairs a <see cref="IProviderAuthenticator.ProviderId"/>
/// with a display name. Only GitHub exists today; the list is still driven by whatever
/// <see cref="IProviderAuthenticator"/>s are registered in DI, not hardcoded in the view, so
/// adding a second provider later is wiring, not a UI rewrite.
/// </summary>
public sealed class ProviderOption(string providerId, string displayName)
{
    public string ProviderId { get; } = providerId;

    public string DisplayName { get; } = displayName;
}

public enum ConnectionState
{
    Idle,
    Connecting,
    Connected,
    Error,
}

/// <summary>
/// View model for the onboarding screen (issue #20): lists available task providers, lets the
/// user connect one via OAuth, and persists the resulting token/schema so the app can skip this
/// screen on the next start. Contains presentation logic only (state transitions, error
/// messages) — the actual auth/schema/storage work is delegated to the injected ports; no
/// business rule lives here.
/// </summary>
public sealed class OnboardingViewModel : ObservableObject
{
    /// <summary>
    /// Display names for known providers. Expand this alongside new <see cref="IProviderAuthenticator"/>
    /// registrations — there's no discovery mechanism for a human-friendly name on the port itself.
    /// </summary>
    private static readonly IReadOnlyDictionary<string, string> ProviderDisplayNames = new Dictionary<string, string>
    {
        ["github"] = "GitHub",
    };

    private const string ConnectedProviderSettingKey = "provider:connected";

    private readonly IReadOnlyDictionary<string, IProviderAuthenticator> _authenticatorsByProviderId;
    private readonly IReadOnlyDictionary<string, ITaskProviderClient> _clientsByProviderId;
    private readonly ICredentialStore _credentialStore;
    private readonly IAppSettingsStore _appSettingsStore;

    private ConnectionState _state = ConnectionState.Idle;
    private string? _errorMessage;
    private string? _connectedProviderId;

    public OnboardingViewModel(
        IEnumerable<IProviderAuthenticator> authenticators,
        IEnumerable<ITaskProviderClient> clients,
        ICredentialStore credentialStore,
        IAppSettingsStore appSettingsStore)
    {
        _authenticatorsByProviderId = authenticators.ToDictionary(a => a.ProviderId);
        _clientsByProviderId = clients.ToDictionary(c => c.ProviderId);
        _credentialStore = credentialStore;
        _appSettingsStore = appSettingsStore;

        Providers = new ObservableCollection<ProviderOption>(
            _authenticatorsByProviderId.Keys.Select(
                id => new ProviderOption(id, ProviderDisplayNames.GetValueOrDefault(id, id))));

        ConnectCommand = new AsyncRelayCommand(param => ConnectAsync((string)param!, CancellationToken.None));
    }

    public ObservableCollection<ProviderOption> Providers { get; }

    public AsyncRelayCommand ConnectCommand { get; }

    public ConnectionState State
    {
        get => _state;
        private set => SetProperty(ref _state, value);
    }

    public string? ErrorMessage
    {
        get => _errorMessage;
        private set => SetProperty(ref _errorMessage, value);
    }

    /// <summary>Provider id that just finished connecting successfully, if any.</summary>
    public string? ConnectedProviderId
    {
        get => _connectedProviderId;
        private set => SetProperty(ref _connectedProviderId, value);
    }

    /// <summary>
    /// Checks whether a provider was already connected in a previous session, so the caller
    /// (see <c>App.xaml.cs</c>) can decide whether to skip this screen entirely.
    /// </summary>
    public async Task<string?> GetAlreadyConnectedProviderIdAsync(CancellationToken cancellationToken)
    {
        return await _appSettingsStore.GetAsync(ConnectedProviderSettingKey, cancellationToken);
    }

    public async Task ConnectAsync(string providerId, CancellationToken cancellationToken)
    {
        State = ConnectionState.Connecting;
        ErrorMessage = null;

        try
        {
            if (!_authenticatorsByProviderId.TryGetValue(providerId, out var authenticator))
            {
                throw new InvalidOperationException($"No authenticator registered for provider '{providerId}'.");
            }

            if (!_clientsByProviderId.TryGetValue(providerId, out var client))
            {
                throw new InvalidOperationException($"No task provider client registered for provider '{providerId}'.");
            }

            var authResult = await authenticator.AuthenticateAsync(cancellationToken);
            await _credentialStore.SaveAsync(TokenSettingKey(providerId), authResult.AccessToken, cancellationToken);
            await _appSettingsStore.SetAsync(ConnectedProviderSettingKey, providerId, cancellationToken);

            ConnectedProviderId = providerId;
            State = ConnectionState.Connected;

            // Best-effort schema pre-fetch, not a connection requirement: providers like GitHub
            // Projects v2 need extra config (which project) that has no selection UI yet, so this
            // is expected to fail until that exists - a login that otherwise succeeded must not
            // be reported as an error, and must not be left un-flagged as connected (that would
            // loop the onboarding screen forever despite a valid saved token). #26 (task creation
            // screen) fetches the schema on demand if it's still missing here.
            try
            {
                var schema = await client.GetTaskSchemaAsync(cancellationToken);
                var schemaJson = JsonSerializer.Serialize(schema);
                await _appSettingsStore.SetAsync(SchemaSettingKey(providerId), schemaJson, cancellationToken);
            }
            catch
            {
                // Swallowed intentionally - see comment above.
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            State = ConnectionState.Error;
        }
    }

    private static string TokenSettingKey(string providerId) => $"provider:{providerId}:token";

    private static string SchemaSettingKey(string providerId) => $"provider:{providerId}:schema";
}
