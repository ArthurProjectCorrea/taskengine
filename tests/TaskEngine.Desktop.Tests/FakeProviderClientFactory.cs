using TaskEngine.Application.Abstractions;

namespace TaskEngine.Desktop.Tests;

/// <summary>
/// Hand-written fake for <see cref="IProviderClientFactory"/>, no mocking library. Wraps
/// pre-built <see cref="ITaskProviderClient"/> fakes (e.g. <see cref="FakeTaskProviderClient"/>)
/// keyed by provider id, resolving them on demand - or throws a fixed failure, to simulate a
/// provider that isn't connected (no token saved).
/// </summary>
public sealed class FakeProviderClientFactory : IProviderClientFactory
{
    private readonly Dictionary<string, ITaskProviderClient> _clientsByProviderId;
    private readonly Exception? _failure;

    public FakeProviderClientFactory(params ITaskProviderClient[] clients)
    {
        _clientsByProviderId = clients.ToDictionary(c => c.ProviderId);
    }

    public FakeProviderClientFactory(Exception failure)
    {
        _clientsByProviderId = [];
        _failure = failure;
    }

    public int CreateCallCount { get; private set; }

    public Task<ITaskProviderClient> CreateAsync(string providerId, CancellationToken cancellationToken)
    {
        CreateCallCount++;

        if (_failure is not null)
        {
            throw _failure;
        }

        if (!_clientsByProviderId.TryGetValue(providerId, out var client))
        {
            throw new InvalidOperationException($"No fake client registered for provider '{providerId}'.");
        }

        return Task.FromResult(client);
    }
}
