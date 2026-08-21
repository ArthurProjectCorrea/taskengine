namespace TaskEngine.Application.Providers;

/// <summary>
/// Thrown when an operation that requires a live connection to <see cref="ProviderId"/> is
/// attempted while that provider is frozen (RF-013/RN-008, ERS-Tarefas.md) - either because the
/// user explicitly disconnected it (<c>DisconnectProviderUseCase</c>), or because a 401 from the
/// provider was detected as an externally revoked access (<c>GitHubIssuesClient</c>). Callers
/// should surface this distinctly (e.g. "reconnect the provider to continue") rather than as a
/// generic failure - see CA-013.1.
/// </summary>
public sealed class ProviderFrozenException : Exception
{
    public string ProviderId { get; }

    public ProviderFrozenException(string providerId)
        : base($"Provider '{providerId}' is frozen (access revoked or explicitly disconnected) and must be reconnected before this action.")
    {
        ProviderId = providerId;
    }
}
