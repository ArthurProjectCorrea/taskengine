namespace TaskEngine.Infrastructure.Providers.GitHub;

/// <summary>
/// Configuration for <see cref="GitHubOAuthAuthenticator"/>. Deliberately has no client secret:
/// the flow is Authorization Code + PKCE for a public client (a GitHub App, not a classic OAuth
/// App), which is the only kind of credential safe to embed in a distributed desktop binary. Where
/// <see cref="ClientId"/> comes from is decided by the composition root.
/// </summary>
/// <param name="ClientId">GitHub App client id - public, sent in the authorize URL.</param>
/// <param name="Scopes">OAuth scopes requested during authorization.</param>
/// <param name="TokenExchangeProxyUrl">
/// Base URL of the token-exchange proxy (see <c>services/oauth-proxy/</c>, a Cloudflare Worker)
/// that <see cref="GitHubOAuthAuthenticator"/> POSTs to instead of GitHub directly when trading an
/// authorization code for an access token. GitHub always requires a <c>client_secret</c> for that
/// step, even with PKCE - unlike Google/Microsoft (see
/// https://github.blog/changelog/2025-07-14-pkce-support-for-oauth-and-github-app-authentication/)
/// - and embedding that secret in this binary would make it trivially extractable, so the proxy
/// holds it instead and this app never sees it. Only the token exchange goes through the proxy;
/// the initial authorize redirect still goes straight to GitHub with the public
/// <see cref="ClientId"/>.
/// </param>
public sealed record GitHubOAuthOptions(string ClientId, IReadOnlyList<string> Scopes, string TokenExchangeProxyUrl);
