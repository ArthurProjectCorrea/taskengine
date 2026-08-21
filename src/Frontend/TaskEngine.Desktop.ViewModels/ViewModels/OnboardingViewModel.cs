using System.Collections.ObjectModel;
using TaskEngine.Application.Abstractions;
using TaskEngine.Desktop.Mvvm;

namespace TaskEngine.Desktop.ViewModels;

/// <summary>
/// One provider entry rendered by <c>OnboardingPage</c> — pairs a <see cref="IProviderAuthenticator.ProviderId"/>
/// with a display name and an optional brand icon. Only GitHub exists today; the list is still driven
/// by whatever <see cref="IProviderAuthenticator"/>s are registered in DI, not hardcoded in the view, so
/// adding a second provider later is wiring, not a UI rewrite.
/// </summary>
public sealed class ProviderOption(string providerId, string displayName, string? iconFileName)
{
    public string ProviderId { get; } = providerId;

    public string DisplayName { get; } = displayName;

    /// <summary>
    /// MAUI image resource file name (see <c>Resources/Images/ProviderIcons/</c>), or <see langword="null"/>
    /// when no icon asset is registered for this provider yet. <c>OnboardingPage.xaml</c> falls back to
    /// <see cref="Initial"/> in a plain colored circle when this is <see langword="null"/>, instead of
    /// rendering a broken image.
    /// </summary>
    public string? IconFileName { get; } = iconFileName;

    /// <summary>First character of <see cref="DisplayName"/>, used by the fallback rendering above.</summary>
    public string Initial { get; } = displayName.Length > 0 ? displayName[..1].ToUpperInvariant() : "?";
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
/// user connect one via OAuth, and persists the resulting token so the app can skip this screen
/// on the next start. Contains presentation logic only (state transitions, error messages) — the
/// actual auth/storage work is delegated to the injected ports; no business rule lives here.
/// </summary>
public sealed class OnboardingViewModel : ObservableObject
{
    /// <summary>
    /// Display names for known providers. Expand this alongside new <see cref="IProviderAuthenticator"/>
    /// registrations — there's no discovery mechanism for a human-friendly name on the port itself.
    /// Internal (not private), same rationale as <see cref="ConnectedProviderSettingKey"/>: reused
    /// as-is by <c>ConfiguracoesViewModel</c> (issue #52) to label the currently connected provider,
    /// instead of duplicating this table there.
    /// </summary>
    internal static readonly IReadOnlyDictionary<string, string> ProviderDisplayNames = new Dictionary<string, string>
    {
        ["github"] = "GitHub",
    };

    /// <summary>
    /// Maps a <see cref="IProviderAuthenticator.ProviderId"/> to the MAUI image resource file name for
    /// its brand icon (downloaded from Simple Icons, https://simpleicons.org, CC0-licensed — see
    /// <c>Resources/Images/ProviderIcons/</c>). A provider missing from this table simply renders the
    /// text-initial fallback (see <see cref="ProviderOption.Initial"/>) instead of a broken image — this
    /// table is expected to grow as new providers ship (Jira/Trello/ClickUp icons are already bundled
    /// ahead of their authenticators existing, since Simple Icons covers them already).
    /// </summary>
    internal static readonly IReadOnlyDictionary<string, string> ProviderIconFiles = new Dictionary<string, string>
    {
        ["github"] = "provider_github.png",
        ["jira"] = "provider_jira.png",
        ["trello"] = "provider_trello.png",
        ["clickup"] = "provider_clickup.png",
    };

    /// <summary>
    /// Internal (not private) so <c>TarefasViewModel</c> can read the same key when deciding which
    /// provider to pass to <c>SyncTasksUseCase</c>, instead of duplicating this literal - there is
    /// no <c>ProviderSettingsKeys</c> helper for "which provider is connected" today (only
    /// per-provider keys - see <c>TaskEngine.Application.Providers.ProviderSettingsKeys</c>), since
    /// this key was never meant to be shared before now.
    /// </summary>
    internal const string ConnectedProviderSettingKey = "provider:connected";

    private readonly IReadOnlyDictionary<string, IProviderAuthenticator> _authenticatorsByProviderId;
    private readonly ICredentialStore _credentialStore;
    private readonly IAppSettingsStore _appSettingsStore;

    private ConnectionState _state = ConnectionState.Idle;
    private string? _errorMessage;
    private string? _connectedProviderId;

    public OnboardingViewModel(
        IEnumerable<IProviderAuthenticator> authenticators,
        ICredentialStore credentialStore,
        IAppSettingsStore appSettingsStore)
    {
        _authenticatorsByProviderId = authenticators.ToDictionary(a => a.ProviderId);
        _credentialStore = credentialStore;
        _appSettingsStore = appSettingsStore;

        Providers = new ObservableCollection<ProviderOption>(
            _authenticatorsByProviderId.Keys.Select(
                id => new ProviderOption(
                    id,
                    ProviderDisplayNames.GetValueOrDefault(id, id),
                    ProviderIconFiles.GetValueOrDefault(id))));

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

            var authResult = await authenticator.AuthenticateAsync(cancellationToken);
            await _credentialStore.SaveAsync(TokenSettingKey(providerId), authResult.AccessToken, cancellationToken);
            await _appSettingsStore.SetAsync(ConnectedProviderSettingKey, providerId, cancellationToken);

            ConnectedProviderId = providerId;
            State = ConnectionState.Connected;
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            State = ConnectionState.Error;
        }
    }

    private static string TokenSettingKey(string providerId) => $"provider:{providerId}:token";
}
