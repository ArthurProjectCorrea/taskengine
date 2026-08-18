namespace TaskEngine.Application.Abstractions;

/// <summary>
/// Port for secure storage of secrets (API keys, tokens, etc.), as opposed to
/// <see cref="IAppSettingsStore"/> which is for non-secret configuration values.
/// Implemented by TaskEngine.Infrastructure using OS-level encryption (issue #17).
/// </summary>
public interface ICredentialStore
{
    Task SaveAsync(string key, string secret, CancellationToken cancellationToken);

    Task<string?> GetAsync(string key, CancellationToken cancellationToken);

    Task DeleteAsync(string key, CancellationToken cancellationToken);
}
