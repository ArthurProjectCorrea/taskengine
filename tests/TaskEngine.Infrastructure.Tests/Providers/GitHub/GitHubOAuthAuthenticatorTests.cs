using System.Web;
using TaskEngine.Infrastructure.Providers.GitHub;

namespace TaskEngine.Infrastructure.Tests.Providers.GitHub;

public class GitHubOAuthAuthenticatorTests
{
    private static GitHubOAuthOptions CreateOptions() => new("test-client-id", ["repo", "project"]);

    [Fact]
    public async Task AuthenticateAsync_SuccessFlow_ReturnsAccessTokenFromTokenEndpoint()
    {
        const string tokenResponse = """
            { "access_token": "gho_abc123", "token_type": "bearer", "scope": "repo,project" }
            """;
        var handler = new FakeHttpMessageHandler(tokenResponse);
        using var httpClient = new HttpClient(handler);
        Uri? capturedAuthorizeUri = null;

        var authenticator = new GitHubOAuthAuthenticator(httpClient, CreateOptions(), browserLauncher: uri =>
        {
            capturedAuthorizeUri = uri;
            _ = Task.Run(() => CompleteBrowserCallbackAsync(uri, code: "auth-code-123"));
        });

        var result = await authenticator.AuthenticateAsync(CancellationToken.None);

        Assert.Equal("github", result.ProviderId);
        Assert.Equal("gho_abc123", result.AccessToken);
        Assert.Equal("repo,project", result.Scope);

        Assert.NotNull(capturedAuthorizeUri);
        var authorizeQuery = HttpUtility.ParseQueryString(capturedAuthorizeUri!.Query);
        Assert.Equal("test-client-id", authorizeQuery["client_id"]);
        Assert.Equal("repo project", authorizeQuery["scope"]);
        Assert.Equal("S256", authorizeQuery["code_challenge_method"]);
        Assert.False(string.IsNullOrEmpty(authorizeQuery["code_challenge"]));
        Assert.False(string.IsNullOrEmpty(authorizeQuery["state"]));

        Assert.Single(handler.CapturedRequestBodies);
        Assert.Contains("auth-code-123", handler.CapturedRequestBodies[0]);
        Assert.Contains("code_verifier", handler.CapturedRequestBodies[0]);
    }

    [Fact]
    public async Task AuthenticateAsync_MismatchedState_ThrowsInvalidOperationException()
    {
        var handler = new FakeHttpMessageHandler();
        using var httpClient = new HttpClient(handler);

        var authenticator = new GitHubOAuthAuthenticator(httpClient, CreateOptions(), browserLauncher: uri =>
        {
            _ = Task.Run(() => CompleteBrowserCallbackAsync(uri, code: "auth-code-123", stateOverride: "not-the-real-state"));
        });

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => authenticator.AuthenticateAsync(CancellationToken.None));
        Assert.Contains("state", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(handler.CapturedRequestBodies);
    }

    [Fact]
    public async Task AuthenticateAsync_UserDeniesAccess_ThrowsInvalidOperationException()
    {
        var handler = new FakeHttpMessageHandler();
        using var httpClient = new HttpClient(handler);

        var authenticator = new GitHubOAuthAuthenticator(httpClient, CreateOptions(), browserLauncher: uri =>
        {
            _ = Task.Run(() => CompleteBrowserCallbackAsync(uri, code: null, error: "access_denied"));
        });

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => authenticator.AuthenticateAsync(CancellationToken.None));
        Assert.Contains("access_denied", exception.Message);
        Assert.Empty(handler.CapturedRequestBodies);
    }

    [Fact]
    public async Task AuthenticateAsync_TokenEndpointReturnsError_ThrowsInvalidOperationException()
    {
        const string errorResponse = """
            { "error": "bad_verification_code", "error_description": "The code passed is incorrect or expired." }
            """;
        var handler = new FakeHttpMessageHandler(errorResponse);
        using var httpClient = new HttpClient(handler);

        var authenticator = new GitHubOAuthAuthenticator(httpClient, CreateOptions(), browserLauncher: uri =>
        {
            _ = Task.Run(() => CompleteBrowserCallbackAsync(uri, code: "auth-code-123"));
        });

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => authenticator.AuthenticateAsync(CancellationToken.None));
        Assert.Contains("bad_verification_code", exception.Message);
    }

    /// <summary>
    /// Stands in for the real browser: reads <c>redirect_uri</c> and <c>state</c> straight out of
    /// the authorize URL the authenticator generated, then makes a real GET against the loopback
    /// listener - exactly what a browser redirect would do after the user authorizes (or denies)
    /// the app on GitHub's real login page.
    /// </summary>
    private static async Task CompleteBrowserCallbackAsync(
        Uri authorizeUri, string? code, string? stateOverride = null, string? error = null)
    {
        var authorizeQuery = HttpUtility.ParseQueryString(authorizeUri.Query);
        var redirectUri = authorizeQuery["redirect_uri"]!;
        var state = stateOverride ?? authorizeQuery["state"]!;

        var callbackQuery = HttpUtility.ParseQueryString(string.Empty);
        if (error is not null)
        {
            callbackQuery["error"] = error;
        }
        else
        {
            callbackQuery["code"] = code;
        }
        callbackQuery["state"] = state;

        var callbackUri = new UriBuilder(redirectUri) { Query = callbackQuery.ToString() }.Uri;

        using var httpClient = new HttpClient();
        await httpClient.GetAsync(callbackUri);
    }
}
