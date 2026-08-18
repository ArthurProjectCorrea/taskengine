namespace TaskEngine.Application.Abstractions;

/// <summary>
/// Port for resolving an <see cref="ITaskProviderClient"/> for a given provider, with its real
/// credentials, at the moment it's needed - rather than constructing one early with a placeholder
/// token (a mistake made and corrected for schema fetching in issue #20; must not be repeated
/// here now that this factory is used to actually create tasks on the provider).
/// </summary>
public interface IProviderClientFactory
{
    Task<ITaskProviderClient> CreateAsync(string providerId, CancellationToken cancellationToken);
}
