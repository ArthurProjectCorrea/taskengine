using System.Diagnostics;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using System.Web;
using TaskEngine.Application.Abstractions;
using TaskEngine.Application.Providers;
using TaskEngine.Infrastructure.Auth;

namespace TaskEngine.Infrastructure.Providers.GitHub;

/// <summary>
/// <see cref="IProviderAuthenticator"/> implementation for GitHub, using Authorization Code + PKCE
/// via a local loopback listener (see <see cref="LoopbackCallbackListener"/>) - no client secret,
/// suitable for a GitHub App registered as a public client.
/// </summary>
public sealed class GitHubOAuthAuthenticator : IProviderAuthenticator
{
    private const string AuthorizeEndpoint = "https://github.com/login/oauth/authorize";
    private const string AccessTokenEndpoint = "https://github.com/login/oauth/access_token";

    /// <summary>32 bytes of randomness, base64url-encoded, for the anti-CSRF `state` parameter.</summary>
    private const int StateEntropyBytes = 32;

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly HttpClient _httpClient;
    private readonly GitHubOAuthOptions _options;
    private readonly Action<Uri> _browserLauncher;

    public GitHubOAuthAuthenticator(HttpClient httpClient, GitHubOAuthOptions options, Action<Uri>? browserLauncher = null)
    {
        _httpClient = httpClient;
        _options = options;
        _browserLauncher = browserLauncher ?? LaunchSystemBrowser;
    }

    public string ProviderId => "github";

    public async Task<ProviderAuthResult> AuthenticateAsync(CancellationToken cancellationToken)
    {
        var pkce = PkceChallenge.Create();
        var state = GenerateState();

        using var listener = new LoopbackCallbackListener();

        var authorizeUri = BuildAuthorizeUri(listener.RedirectUri, pkce, state);
        _browserLauncher(authorizeUri);

        var callback = await listener.WaitForCallbackAsync(cancellationToken);

        var error = callback["error"];
        if (!string.IsNullOrEmpty(error))
        {
            throw new InvalidOperationException($"GitHub login was cancelled or denied by the user: {error}.");
        }

        var receivedState = callback["state"];
        if (!string.Equals(receivedState, state, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "GitHub login callback 'state' did not match the value that was sent (possible CSRF).");
        }

        var code = callback["code"]
            ?? throw new InvalidOperationException("GitHub login callback did not include an authorization code.");

        var tokenResponse = await ExchangeCodeForTokenAsync(code, pkce.CodeVerifier, listener.RedirectUri, cancellationToken);

        return new ProviderAuthResult(ProviderId, tokenResponse.AccessToken!, tokenResponse.Scope, DateTimeOffset.UtcNow);
    }

    private Uri BuildAuthorizeUri(Uri redirectUri, PkceChallenge pkce, string state)
    {
        var query = HttpUtility.ParseQueryString(string.Empty);
        query["client_id"] = _options.ClientId;
        query["redirect_uri"] = redirectUri.ToString();
        query["scope"] = string.Join(' ', _options.Scopes);
        query["state"] = state;
        query["code_challenge"] = pkce.CodeChallenge;
        query["code_challenge_method"] = "S256";

        return new UriBuilder(AuthorizeEndpoint) { Query = query.ToString() }.Uri;
    }

    private async Task<GitHubAccessTokenResponseDto> ExchangeCodeForTokenAsync(
        string code, string codeVerifier, Uri redirectUri, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, AccessTokenEndpoint)
        {
            Content = JsonContent.Create(new
            {
                client_id = _options.ClientId,
                code,
                redirect_uri = redirectUri.ToString(),
                code_verifier = codeVerifier,
            }),
        };
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<GitHubAccessTokenResponseDto>(SerializerOptions, cancellationToken)
            ?? throw new InvalidOperationException("GitHub token endpoint returned an empty response.");

        if (!string.IsNullOrEmpty(body.Error))
        {
            throw new InvalidOperationException($"GitHub token exchange failed: {body.Error} - {body.ErrorDescription}");
        }

        if (string.IsNullOrEmpty(body.AccessToken))
        {
            throw new InvalidOperationException("GitHub token endpoint did not return an access token.");
        }

        return body;
    }

    private static string GenerateState()
    {
        var bytes = RandomNumberGenerator.GetBytes(StateEntropyBytes);
        return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    private static void LaunchSystemBrowser(Uri uri) =>
        Process.Start(new ProcessStartInfo(uri.ToString()) { UseShellExecute = true });
}
