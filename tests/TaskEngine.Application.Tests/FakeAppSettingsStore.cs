using TaskEngine.Application.Abstractions;

namespace TaskEngine.Application.Tests;

/// <summary>Hand-written in-memory fake for <see cref="IAppSettingsStore"/>, no mocking library.</summary>
public sealed class FakeAppSettingsStore : IAppSettingsStore
{
    private readonly Dictionary<string, string> _values = [];

    public IReadOnlyDictionary<string, string> Values => _values;

    public Task<string?> GetAsync(string key, CancellationToken cancellationToken)
    {
        return Task.FromResult(_values.GetValueOrDefault(key));
    }

    public Task SetAsync(string key, string value, CancellationToken cancellationToken)
    {
        _values[key] = value;
        return Task.CompletedTask;
    }

    public Task DeleteAsync(string key, CancellationToken cancellationToken)
    {
        _values.Remove(key);
        return Task.CompletedTask;
    }
}
