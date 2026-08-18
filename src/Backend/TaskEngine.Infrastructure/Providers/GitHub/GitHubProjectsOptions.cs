namespace TaskEngine.Infrastructure.Providers.GitHub;

/// <summary>
/// Connection details for a GitHub Projects v2 board. Where <see cref="AccessToken"/> comes from
/// (OAuth login, issue #22; secure storage, issue #17) is decided by the composition root — this
/// client only needs a valid token, not how it was obtained.
/// </summary>
public sealed record GitHubProjectsOptions(string AccessToken, string OwnerLogin, int ProjectNumber);
