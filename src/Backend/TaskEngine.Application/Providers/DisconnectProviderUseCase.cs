using TaskEngine.Application.Abstractions;

namespace TaskEngine.Application.Providers;

/// <summary>
/// Explicitly disconnects a provider (RF-013/RN-008, ERS-Tarefas.md/ERS-Configuracoes.md): freezes
/// it first (so sync/conclusion are blocked even if credential deletion fails partway through -
/// RNF-003 style "no partial bad state"), then deletes the saved access token via
/// <see cref="ICredentialStore"/>. Local task/time data is left untouched - RF-013 only blocks
/// sync and conclusion, it does not delete anything. The "which provider is connected" setting
/// (owned by the Desktop onboarding flow) is intentionally left alone too, since reconnecting to
/// the same provider is the expected path back (<c>ReconnectProviderUseCase</c>), not picking a
/// different one.
/// </summary>
public sealed class DisconnectProviderUseCase
{
    private readonly ICredentialStore _credentialStore;
    private readonly IAppSettingsStore _appSettingsStore;

    public DisconnectProviderUseCase(ICredentialStore credentialStore, IAppSettingsStore appSettingsStore)
    {
        _credentialStore = credentialStore;
        _appSettingsStore = appSettingsStore;
    }

    public async Task ExecuteAsync(string providerId, CancellationToken cancellationToken = default)
    {
        await _appSettingsStore.SetAsync(ProviderSettingsKeys.Frozen(providerId), "true", cancellationToken);
        await _credentialStore.DeleteAsync(ProviderSettingsKeys.CredentialKey(providerId), cancellationToken);
    }
}
