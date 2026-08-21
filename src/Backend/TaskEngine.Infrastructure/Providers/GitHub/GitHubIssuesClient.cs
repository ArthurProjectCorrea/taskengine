using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using TaskEngine.Application.Abstractions;
using TaskEngine.Application.Providers;

using TaskStatus = TaskEngine.Domain.Entities.TaskStatus;

namespace TaskEngine.Infrastructure.Providers.GitHub;

/// <summary>
/// <see cref="ITaskProviderClient"/> implementation backed by the GitHub Issues Search API
/// (GraphQL <c>search(type: ISSUE)</c>), not GitHub Projects v2. Projects v2 required the user to
/// pick an owner and a project number before any task could be synced (no UI ever existed for
/// that selection, so sync was permanently blocked - see issue history on
/// <c>ProviderClientFactory</c>). Searching <c>is:issue assignee:@me</c> instead returns every
/// issue assigned to the authenticated user across every repository they can see, with zero
/// configuration. The tradeoff: plain Issues have no custom fields, so "in progress" and
/// "Priority" are approximated rather than read from a real field (see
/// <see cref="ListAssignedTasksAsync"/> and <see cref="ExtractPriority"/>).
/// </summary>
public sealed class GitHubIssuesClient : ITaskProviderClient
{
    private const string Endpoint = "https://api.github.com/graphql";

    /// <summary>
    /// <c>archived:false</c> excludes issues in archived repositories - those can't meaningfully
    /// be worked on, so surfacing them as assigned tasks would just be noise. Everything else
    /// (open or closed, any repository the token can see) is returned; <see cref="ListAssignedTasksAsync"/>
    /// maps <c>state</c> to <see cref="ProviderTaskSummary.IsDone"/>.
    /// </summary>
    private const string AssignedToViewerQueryString = "is:issue assignee:@me archived:false";

    private const string SearchQuery = """
        query($queryString: String!, $after: String) {
          search(query: $queryString, type: ISSUE, first: 100, after: $after) {
            pageInfo { hasNextPage endCursor }
            nodes {
              ... on Issue {
                id
                title
                body
                createdAt
                url
                state
                labels(first: 20) { nodes { name } }
              }
            }
          }
        }
        """;

    private const string CloseIssueMutation = """
        mutation($id: ID!) {
          closeIssue(input: { issueId: $id }) {
            issue { id state }
          }
        }
        """;

    private const string ReopenIssueMutation = """
        mutation($id: ID!) {
          reopenIssue(input: { issueId: $id }) {
            issue { id state }
          }
        }
        """;

    private const string AddCommentMutation = """
        mutation($id: ID!, $body: String!) {
          addComment(input: { subjectId: $id, body: $body }) {
            commentEdge { node { id } }
          }
        }
        """;

    /// <summary>
    /// Safety cap on how many assigned issues <see cref="ListAssignedTasksAsync"/> will page
    /// through - not a product limit, just a guard against an unbounded loop if GitHub ever
    /// returned a <c>pageInfo</c> that never settles.
    /// </summary>
    private const int MaxIssues = 500;

    private const int PageSize = 100;

    /// <summary>
    /// Best-effort, case-insensitive label-to-priority mapping - GitHub Issues (outside Projects
    /// v2) have no native "Priority" field. Recognizes: a <c>priority:&lt;value&gt;</c> prefix
    /// (e.g. <c>"priority: high"</c> -> <c>"high"</c>), a bare <c>P0</c>-<c>P3</c> label, or one of
    /// these exact names. If a task has multiple recognized labels, the first one found (in the
    /// order returned by GitHub) wins. If none match, <see cref="ProviderTaskSummary.Priority"/>
    /// is null - same "best effort, not guaranteed" spirit as the old Projects v2 client's
    /// StatusOptionNames/TimeFieldNames matching.
    /// </summary>
    private static readonly string[] RecognizedPriorityNames = ["critical", "high", "medium", "low"];

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly HttpClient _httpClient;
    private readonly GitHubIssuesOptions _options;
    private readonly IAppSettingsStore _appSettingsStore;

    /// <summary>
    /// <paramref name="appSettingsStore"/> lets this client mark the provider frozen (RF-013,
    /// <see cref="ProviderSettingsKeys.Frozen"/>) the moment a 401 is observed - detecting a
    /// revoked token as close to the source as possible, rather than requiring every caller to
    /// catch-and-freeze individually.
    /// </summary>
    public GitHubIssuesClient(HttpClient httpClient, GitHubIssuesOptions options, IAppSettingsStore appSettingsStore)
    {
        _httpClient = httpClient;
        _options = options;
        _appSettingsStore = appSettingsStore;
    }

    public string ProviderId => "github";

    /// <summary>
    /// Pages through <c>search(query: "is:issue assignee:@me archived:false")</c> (RF-001) until
    /// GitHub reports no more pages or <see cref="MaxIssues"/> is reached. No client-side assignee
    /// filtering is needed - <c>assignee:@me</c> is resolved server-side against the token's
    /// owner.
    /// </summary>
    public async Task<IReadOnlyList<ProviderTaskSummary>> ListAssignedTasksAsync(CancellationToken cancellationToken)
    {
        var summaries = new List<ProviderTaskSummary>();
        string? cursor = null;

        do
        {
            var data = await ExecuteAsync<SearchResponseDto>(
                SearchQuery,
                new { queryString = AssignedToViewerQueryString, after = cursor },
                cancellationToken);

            var connection = data.Search
                ?? throw new InvalidOperationException("GitHub GraphQL response had no 'search' field.");

            foreach (var issue in connection.Nodes)
            {
                if (issue.Id is null || issue.Title is null)
                {
                    // No content resolved for this node (e.g. a redacted result this token can't
                    // fully see) - skip.
                    continue;
                }

                var isDone = string.Equals(issue.State, "CLOSED", StringComparison.OrdinalIgnoreCase);

                summaries.Add(new ProviderTaskSummary(
                    ExternalId: issue.Id,
                    Title: issue.Title,
                    Description: issue.Body,
                    StatusName: issue.State?.ToLowerInvariant(),
                    // Plain GitHub Issues only model open/closed - "in progress" has no
                    // provider-side signal and is left for the app to decide locally once a work
                    // session starts (same as RN-011's existing local classification).
                    IsInProgress: false,
                    IsDone: isDone,
                    CreatedAt: issue.CreatedAt ?? DateTimeOffset.UtcNow,
                    Priority: ExtractPriority(issue.Labels?.Nodes),
                    Url: issue.Url));
            }

            cursor = connection.PageInfo.HasNextPage ? connection.PageInfo.EndCursor : null;
        }
        while (cursor is not null && summaries.Count < MaxIssues);

        return summaries;
    }

    /// <summary>
    /// GitHub Issues (outside Projects v2) only model a binary open/closed state - there is no
    /// native "in progress"/"paused" transition to push back. <see cref="TaskStatus.Done"/> and
    /// <see cref="TaskStatus.DonePendingSync"/> close the issue; every other status
    /// (<see cref="TaskStatus.ToDo"/>/<see cref="TaskStatus.InProgress"/>/<see cref="TaskStatus.Paused"/>)
    /// reopens it. Both <c>closeIssue</c> and <c>reopenIssue</c> are idempotent on GitHub's side -
    /// closing an already-closed issue, or reopening an already-open one, is a no-op success - so
    /// no current-state check is needed first.
    /// </summary>
    public async Task UpdateStatusAsync(ProviderTaskReference reference, TaskStatus status, CancellationToken cancellationToken)
    {
        var mutation = status is TaskStatus.Done or TaskStatus.DonePendingSync
            ? CloseIssueMutation
            : ReopenIssueMutation;

        await ExecuteAsync<EmptyDataDto>(mutation, new { id = reference.ExternalId }, cancellationToken);
    }

    /// <summary>
    /// Plain GitHub Issues have no configurable Number field to record time invested (that was a
    /// Projects v2-only concept). Instead: close the issue (same as <see cref="UpdateStatusAsync"/>
    /// with <see cref="TaskStatus.Done"/>) and post the invested time as a comment - a comment
    /// always works, regardless of how the repository/project is set up, unlike a custom field
    /// that only existed if someone configured it.
    /// </summary>
    public async Task ReportCompletionAsync(ProviderTaskReference reference, TimeSpan totalDuration, CancellationToken cancellationToken)
    {
        await UpdateStatusAsync(reference, TaskStatus.Done, cancellationToken);

        var body = FormatInvestedTimeComment(totalDuration);
        await ExecuteAsync<EmptyDataDto>(AddCommentMutation, new { id = reference.ExternalId, body }, cancellationToken);
    }

    private static string FormatInvestedTimeComment(TimeSpan duration)
    {
        var hours = (int)duration.TotalHours;
        var minutes = duration.Minutes;

        var formatted = (hours, minutes) switch
        {
            (0, var m) => $"{m}min",
            (var h, 0) => $"{h}h",
            (var h, var m) => $"{h}h {m}min",
        };

        return $"Tempo investido (TaskEngine): {formatted}";
    }

    private static string? ExtractPriority(List<LabelNodeDto>? labels)
    {
        if (labels is not { Count: > 0 })
        {
            return null;
        }

        foreach (var label in labels)
        {
            var name = label.Name?.Trim();
            if (string.IsNullOrEmpty(name))
            {
                continue;
            }

            if (name.StartsWith("priority:", StringComparison.OrdinalIgnoreCase))
            {
                var value = name[(name.IndexOf(':') + 1)..].Trim();
                if (value.Length > 0)
                {
                    return value;
                }

                continue;
            }

            if (name.Length == 2
                && (name[0] == 'P' || name[0] == 'p')
                && name[1] is >= '0' and <= '3')
            {
                return $"P{name[1]}";
            }

            var lower = name.ToLowerInvariant();
            if (Array.IndexOf(RecognizedPriorityNames, lower) >= 0)
            {
                return lower;
            }
        }

        return null;
    }

    /// <summary>Executes a query/mutation and throws if GitHub reports any GraphQL error.</summary>
    private async Task<TData> ExecuteAsync<TData>(string query, object? variables, CancellationToken cancellationToken)
    {
        var envelope = await SendAsync<TData>(query, variables, cancellationToken);

        if (envelope?.Errors is { Count: > 0 })
        {
            throw new InvalidOperationException(
                $"GitHub GraphQL error: {string.Join("; ", envelope.Errors.Select(e => e.Message))}");
        }

        return envelope is { Data: not null }
            ? envelope.Data
            : throw new InvalidOperationException("GitHub GraphQL response had no data.");
    }

    private async Task<GraphQlEnvelope<TData>?> SendAsync<TData>(string query, object? variables, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, Endpoint)
        {
            Content = JsonContent.Create(new GraphQlRequestBody(query, variables)),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.AccessToken);
        request.Headers.UserAgent.ParseAdd("TaskEngine");

        using var response = await _httpClient.SendAsync(request, cancellationToken);

        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            // RF-013/RN-008: a 401 here means the token was revoked directly on the provider
            // (the user never disconnected through TaskEngine) - freeze it the same way an
            // explicit disconnect would, so sync/conclusion are blocked until reconnected.
            await _appSettingsStore.SetAsync(ProviderSettingsKeys.Frozen(ProviderId), "true", cancellationToken);
            throw new ProviderFrozenException(ProviderId);
        }

        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<GraphQlEnvelope<TData>>(SerializerOptions, cancellationToken);
    }
}
