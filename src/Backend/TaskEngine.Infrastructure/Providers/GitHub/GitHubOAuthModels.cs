using System.Text.Json.Serialization;

namespace TaskEngine.Infrastructure.Providers.GitHub;

/// <summary>
/// Shape of <c>POST https://github.com/login/oauth/access_token</c>. GitHub returns HTTP 200 with
/// an <see cref="Error"/> payload for some failures instead of a non-success status code, so both
/// the success and error fields live on the same DTO.
/// </summary>
internal sealed class GitHubAccessTokenResponseDto
{
    [JsonPropertyName("access_token")]
    public string? AccessToken { get; set; }

    [JsonPropertyName("token_type")]
    public string? TokenType { get; set; }

    public string? Scope { get; set; }

    public string? Error { get; set; }

    [JsonPropertyName("error_description")]
    public string? ErrorDescription { get; set; }
}
