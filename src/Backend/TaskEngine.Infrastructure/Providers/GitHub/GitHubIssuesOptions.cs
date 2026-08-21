namespace TaskEngine.Infrastructure.Providers.GitHub;

/// <summary>
/// Connection details for the GitHub Issues Search API. Where <see cref="AccessToken"/> comes
/// from (OAuth login, issue #22; secure storage, issue #17) is decided by the composition root -
/// this client only needs a valid token, not how it was obtained. Unlike the Projects v2 client it
/// replaced, there is no owner/project number to configure: <c>search(query: "is:issue
/// assignee:@me")</c> already scopes results to the authenticated user across every repository
/// they have access to.
/// </summary>
public sealed record GitHubIssuesOptions(string AccessToken);
