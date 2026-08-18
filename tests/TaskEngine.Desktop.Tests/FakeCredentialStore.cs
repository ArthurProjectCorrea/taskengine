using TaskEngine.Application.Abstractions;

namespace TaskEngine.Desktop.Tests;

/// <summary>Hand-written in-memory fake for <see cref="ICredentialStore"/>, no mocking library.</summary>
public sealed class FakeCredentialStore : ICredentialStore
{
    private readonly Dictionary<string, string> _secrets = [];

    public IReadOnlyDictionary<string, string> Secrets => _secrets;

    public Task SaveAsync(string key, string secret, CancellationToken cancellationToken)
    {
        _secrets[key] = secret;
        return Task.CompletedTask;
    }

    public Task<string?> GetAsync(string key, CancellationToken cancellationToken)
    {
        return Task.FromResult(_secrets.GetValueOrDefault(key));
    }

    public Task DeleteAsync(string key, CancellationToken cancellationToken)
    {
        _secrets.Remove(key);
        return Task.CompletedTask;
    }
}
