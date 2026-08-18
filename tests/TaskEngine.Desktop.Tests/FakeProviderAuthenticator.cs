using TaskEngine.Application.Abstractions;
using TaskEngine.Application.Providers;

namespace TaskEngine.Desktop.Tests;

/// <summary>
/// Hand-written fake for <see cref="IProviderAuthenticator"/>, no mocking library involved
/// (same pattern as the fakes in TaskEngine.Application.Tests/TaskEngine.Infrastructure.Tests).
/// </summary>
public sealed class FakeProviderAuthenticator : IProviderAuthenticator
{
    private readonly Func<CancellationToken, Task<ProviderAuthResult>>? _authenticate;
    private readonly Exception? _failure;

    public FakeProviderAuthenticator(string providerId, ProviderAuthResult result)
    {
        ProviderId = providerId;
        _authenticate = _ => Task.FromResult(result);
    }

    public FakeProviderAuthenticator(string providerId, Exception failure)
    {
        ProviderId = providerId;
        _failure = failure;
    }

    public string ProviderId { get; }

    public int AuthenticateCallCount { get; private set; }

    public Task<ProviderAuthResult> AuthenticateAsync(CancellationToken cancellationToken)
    {
        AuthenticateCallCount++;

        if (_failure is not null)
        {
            throw _failure;
        }

        return _authenticate!(cancellationToken);
    }
}
