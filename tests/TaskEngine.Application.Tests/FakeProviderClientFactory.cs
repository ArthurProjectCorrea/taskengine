using TaskEngine.Application.Abstractions;

namespace TaskEngine.Application.Tests;

/// <summary>
/// Hand-written fake for <see cref="IProviderClientFactory"/>, no mocking library involved.
/// Records the requested provider id and always returns <see cref="ClientToReturn"/>.
/// </summary>
public sealed class FakeProviderClientFactory : IProviderClientFactory
{
    public FakeTaskProviderClient ClientToReturn { get; set; } = new();

    public string? LastRequestedProviderId { get; private set; }

    public Task<ITaskProviderClient> CreateAsync(string providerId, CancellationToken cancellationToken)
    {
        LastRequestedProviderId = providerId;
        return Task.FromResult<ITaskProviderClient>(ClientToReturn);
    }
}
