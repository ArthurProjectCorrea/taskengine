namespace TaskEngine.Infrastructure.Providers.GitHub;

/// <summary>
/// Configuration for <see cref="GitHubOAuthAuthenticator"/>. Deliberately has no client secret:
/// the flow is Authorization Code + PKCE for a public client (a GitHub App, not a classic OAuth
/// App), which is the only kind of credential safe to embed in a distributed desktop binary. Where
/// <see cref="ClientId"/> comes from is decided by the composition root.
/// </summary>
public sealed record GitHubOAuthOptions(string ClientId, IReadOnlyList<string> Scopes);
