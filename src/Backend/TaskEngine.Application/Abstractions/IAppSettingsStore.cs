namespace TaskEngine.Application.Abstractions;

/// <summary>
/// Port for a generic key/value settings store, used for app configuration and (later, issue #17)
/// encrypted credential storage. Implemented by TaskEngine.Infrastructure.
/// </summary>
public interface IAppSettingsStore
{
    Task<string?> GetAsync(string key, CancellationToken cancellationToken);

    Task SetAsync(string key, string value, CancellationToken cancellationToken);
}
