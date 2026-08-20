using System.Globalization;

namespace TaskEngine.Application.Providers;

/// <summary>
/// Single source of truth for the <see cref="Abstractions.IAppSettingsStore"/>/
/// <see cref="Abstractions.ICredentialStore"/> key formats used for provider connection state,
/// shared by every Application use case and Infrastructure client that needs to read/write it
/// (<c>SyncTasksUseCase</c>, <c>DisconnectProviderUseCase</c>, <c>ReconnectProviderUseCase</c>,
/// <c>GitHubProjectsClient</c>). <see cref="CredentialKey"/> intentionally matches the format the
/// Desktop onboarding flow already uses for the saved access token
/// (<c>OnboardingViewModel.TokenSettingKey</c>/<c>ProviderClientFactory</c>) - that call site is
/// Frontend/out of scope to change, so this mirrors it rather than the other way around.
/// </summary>
public static class ProviderSettingsKeys
{
    public static string CredentialKey(string providerId) =>
        string.Format(CultureInfo.InvariantCulture, "provider:{0}:token", providerId);

    /// <summary>
    /// When the provider was first connected (RN-001) - not currently written by the Desktop
    /// onboarding flow, so <c>SyncTasksUseCase</c> establishes it itself on first use.
    /// </summary>
    public static string ConnectedAt(string providerId) =>
        string.Format(CultureInfo.InvariantCulture, "provider:{0}:connectedAt", providerId);

    /// <summary>
    /// Presence (value <c>"true"</c>) means the provider is frozen (RF-013/RN-008): access was
    /// revoked, either explicitly (<c>DisconnectProviderUseCase</c>) or detected from the provider
    /// (a 401 response - see <c>GitHubProjectsClient</c>). Absence means connected/unfrozen.
    /// </summary>
    public static string Frozen(string providerId) =>
        string.Format(CultureInfo.InvariantCulture, "provider:{0}:frozen", providerId);
}
