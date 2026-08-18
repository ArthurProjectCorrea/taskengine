namespace TaskEngine.Application.Providers;

/// <summary>
/// The outcome of an OAuth login flow for a task provider (see <see cref="Abstractions.IProviderAuthenticator"/>).
/// </summary>
public sealed record ProviderAuthResult(string ProviderId, string AccessToken, string? Scope, DateTimeOffset ObtainedAt);
