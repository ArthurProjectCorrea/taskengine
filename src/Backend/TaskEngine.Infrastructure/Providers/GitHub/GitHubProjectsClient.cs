using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using TaskEngine.Application.Abstractions;
using TaskEngine.Application.Providers;
using TaskEngine.Domain.Entities;

using TaskStatus = TaskEngine.Domain.Entities.TaskStatus;

namespace TaskEngine.Infrastructure.Providers.GitHub;

/// <summary>
/// <see cref="ITaskProviderClient"/> implementation for GitHub Projects v2 (GraphQL API), not
/// plain GitHub Issues (REST) — Projects v2 is what exposes per-project configurable custom
/// fields (Status, Priority, Estimate, etc.), which is what the dynamic schema in
/// <see cref="ITaskProviderClient.GetTaskSchemaAsync"/> is for.
/// </summary>
public sealed class GitHubProjectsClient : ITaskProviderClient
{
    private const string Endpoint = "https://api.github.com/graphql";

    /// <summary>
    /// Separate per-owner-kind queries, tried in sequence (see <see cref="FetchProjectWithFieldsAsync"/>)
    /// rather than one combined query. GitHub's GraphQL API returns a top-level error (not just a
    /// null field) when <c>organization(login:)</c> doesn't resolve, so probing both owner kinds
    /// in a single request made a valid "it's a user, not an org" response look like a hard
    /// failure - each request only asks for the owner kind it expects.
    /// </summary>
    private const string UserSchemaQuery = """
        query($login: String!, $number: Int!) {
          user(login: $login) {
            projectV2(number: $number) {
              id
              fields(first: 50) {
                nodes {
                  ... on ProjectV2FieldCommon { id name dataType }
                  ... on ProjectV2SingleSelectField { id name options { id name } }
                }
              }
            }
          }
        }
        """;

    private const string OrganizationSchemaQuery = """
        query($login: String!, $number: Int!) {
          organization(login: $login) {
            projectV2(number: $number) {
              id
              fields(first: 50) {
                nodes {
                  ... on ProjectV2FieldCommon { id name dataType }
                  ... on ProjectV2SingleSelectField { id name options { id name } }
                }
              }
            }
          }
        }
        """;

    private const string AddDraftIssueMutation = """
        mutation($projectId: ID!, $title: String!, $body: String) {
          addProjectV2DraftIssue(input: { projectId: $projectId, title: $title, body: $body }) {
            projectItem { id }
          }
        }
        """;

    private const string UpdateFieldValueMutation = """
        mutation($projectId: ID!, $itemId: ID!, $fieldId: ID!, $value: ProjectV2FieldValue!) {
          updateProjectV2ItemFieldValue(input: { projectId: $projectId, itemId: $itemId, fieldId: $fieldId, value: $value }) {
            projectV2Item { id }
          }
        }
        """;

    private const string ViewerLoginQuery = "query { viewer { login } }";

    /// <summary>
    /// Projects v2 has no server-side "assigned to viewer" filter on <c>items</c>, so every item
    /// in the project is fetched and filtered by <see cref="AssigneeDto.Login"/> client-side (see
    /// <see cref="ListAssignedTasksAsync"/>). <c>fieldValues</c> only asks for the
    /// <c>ProjectV2ItemFieldSingleSelectValue</c> shape (Status/Priority in this codebase's usage
    /// are both single-select) - other field value kinds are simply omitted from the response,
    /// same GraphQL behavior already relied on by <see cref="FieldNodeDto"/>.
    /// </summary>
    private const string UserAssignedItemsQuery = """
        query($login: String!, $number: Int!) {
          user(login: $login) {
            projectV2(number: $number) {
              items(first: 100) {
                nodes {
                  id
                  fieldValues(first: 20) {
                    nodes {
                      ... on ProjectV2ItemFieldSingleSelectValue {
                        name
                        field { ... on ProjectV2FieldCommon { name } }
                      }
                    }
                  }
                  content {
                    ... on Issue {
                      title
                      body
                      createdAt
                      url
                      assignees(first: 10) { nodes { login } }
                    }
                    ... on DraftIssue {
                      title
                      body
                      createdAt
                      assignees(first: 10) { nodes { login } }
                    }
                  }
                }
              }
            }
          }
        }
        """;

    private const string OrganizationAssignedItemsQuery = """
        query($login: String!, $number: Int!) {
          organization(login: $login) {
            projectV2(number: $number) {
              items(first: 100) {
                nodes {
                  id
                  fieldValues(first: 20) {
                    nodes {
                      ... on ProjectV2ItemFieldSingleSelectValue {
                        name
                        field { ... on ProjectV2FieldCommon { name } }
                      }
                    }
                  }
                  content {
                    ... on Issue {
                      title
                      body
                      createdAt
                      url
                      assignees(first: 10) { nodes { login } }
                    }
                    ... on DraftIssue {
                      title
                      body
                      createdAt
                      assignees(first: 10) { nodes { login } }
                    }
                  }
                }
              }
            }
          }
        }
        """;

    /// <summary>
    /// GitHub's default Status field has configurable option *names* per project — this is a
    /// best-effort match against common names, not a guaranteed mapping. If the user renamed
    /// their columns to something unrecognizable, <see cref="UpdateStatusAsync"/> throws.
    /// </summary>
    private static readonly Dictionary<TaskStatus, string[]> StatusOptionNames = new()
    {
        [TaskStatus.ToDo] = ["Todo", "To Do", "Backlog"],
        [TaskStatus.InProgress] = ["In Progress", "Doing"],
        [TaskStatus.Done] = ["Done", "Closed", "Complete"],
        // "Paused" has no provider-side option name - it is a local classification derived from
        // any status other than in-progress/done (RN-011), not something the system pushes back
        // to the provider.
        [TaskStatus.Paused] = [],
    };

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly HttpClient _httpClient;
    private readonly GitHubProjectsOptions _options;
    private readonly IAppSettingsStore _appSettingsStore;

    /// <summary>
    /// <paramref name="appSettingsStore"/> lets this client mark the provider frozen (RF-013,
    /// <see cref="ProviderSettingsKeys.Frozen"/>) the moment a 401 is observed - detecting a
    /// revoked token as close to the source as possible, rather than requiring every caller to
    /// catch-and-freeze individually.
    /// </summary>
    public GitHubProjectsClient(HttpClient httpClient, GitHubProjectsOptions options, IAppSettingsStore appSettingsStore)
    {
        _httpClient = httpClient;
        _options = options;
        _appSettingsStore = appSettingsStore;
    }

    public string ProviderId => "github";

    public async Task<ProviderTaskSchema> GetTaskSchemaAsync(CancellationToken cancellationToken)
    {
        var project = await FetchProjectWithFieldsAsync(cancellationToken);

        return new ProviderTaskSchema(ProviderId, MapFields(project.Fields.Nodes));
    }

    public async Task<ProviderTaskReference> CreateTaskAsync(
        TaskItem task,
        IReadOnlyDictionary<string, string>? fieldValues,
        CancellationToken cancellationToken)
    {
        var project = await FetchProjectWithFieldsAsync(cancellationToken);

        var addData = await ExecuteAsync<AddDraftIssueResponseDto>(
            AddDraftIssueMutation,
            new { projectId = project.Id, title = task.Title, body = task.Description },
            cancellationToken);

        var itemId = addData.AddProjectV2DraftIssue?.ProjectItem?.Id
            ?? throw new InvalidOperationException("GitHub did not return the created project item id.");

        if (fieldValues is { Count: > 0 })
        {
            foreach (var (key, rawValue) in fieldValues)
            {
                var field = project.Fields.Nodes.FirstOrDefault(f => f.Id == key);
                var type = field is null ? null : MapDataType(field.DataType);
                if (field is null || type is null)
                {
                    // Unknown/unsupported field key - ignore rather than fail the whole creation.
                    continue;
                }

                var value = BuildFieldValuePayload(type.Value, rawValue);
                await ExecuteAsync<EmptyDataDto>(
                    UpdateFieldValueMutation,
                    new { projectId = project.Id, itemId, fieldId = field.Id, value },
                    cancellationToken);
            }
        }

        return new ProviderTaskReference(ProviderId, itemId, Url: null);
    }

    public async Task UpdateStatusAsync(ProviderTaskReference reference, TaskStatus status, CancellationToken cancellationToken)
    {
        var project = await FetchProjectWithFieldsAsync(cancellationToken);

        var statusField = project.Fields.Nodes.FirstOrDefault(
            f => string.Equals(f.Name, "Status", StringComparison.OrdinalIgnoreCase) && f.Options is { Count: > 0 })
            ?? throw new InvalidOperationException("This GitHub project has no 'Status' single-select field configured.");

        var candidateNames = StatusOptionNames[status];
        var option = statusField.Options!.FirstOrDefault(
            o => candidateNames.Any(name => string.Equals(o.Name, name, StringComparison.OrdinalIgnoreCase)))
            ?? throw new InvalidOperationException(
                $"No Status option matching '{status}' was found (looked for: {string.Join(", ", candidateNames)}).");

        var value = new { singleSelectOptionId = option.Id };

        await ExecuteAsync<EmptyDataDto>(
            UpdateFieldValueMutation,
            new { projectId = project.Id, itemId = reference.ExternalId, fieldId = statusField.Id, value },
            cancellationToken);
    }

    public async Task<IReadOnlyList<ProviderTaskSummary>> ListAssignedTasksAsync(CancellationToken cancellationToken)
    {
        var viewerLogin = await GetViewerLoginAsync(cancellationToken);
        List<ProjectItemNodeDto> items = await FetchProjectItemsAsync(cancellationToken);

        var summaries = new List<ProviderTaskSummary>();
        foreach (var item in items)
        {
            if (item.Content is not { Title: not null } content)
            {
                // No linked content (e.g. a redacted item this token can't see) - skip.
                continue;
            }

            var isAssignedToViewer = content.Assignees?.Nodes.Any(
                a => string.Equals(a.Login, viewerLogin, StringComparison.OrdinalIgnoreCase)) ?? false;
            if (!isAssignedToViewer)
            {
                continue;
            }

            var (statusName, priority) = ReadStatusAndPriority(item.FieldValues.Nodes);
            var isInProgress = MatchesStatusOption(TaskStatus.InProgress, statusName);
            var isDone = MatchesStatusOption(TaskStatus.Done, statusName);

            summaries.Add(new ProviderTaskSummary(
                ExternalId: item.Id,
                Title: content.Title,
                Description: content.Body,
                StatusName: statusName,
                IsInProgress: isInProgress,
                IsDone: isDone,
                CreatedAt: content.CreatedAt ?? DateTimeOffset.UtcNow,
                Priority: priority,
                Url: content.Url));
        }

        return summaries;
    }

    private static (string? StatusName, string? Priority) ReadStatusAndPriority(List<FieldValueNodeDto> fieldValues)
    {
        string? statusName = null;
        string? priority = null;

        foreach (var fieldValue in fieldValues)
        {
            var fieldName = fieldValue.Field?.Name;
            if (fieldName is null || fieldValue.Name is null)
            {
                continue;
            }

            if (string.Equals(fieldName, "Status", StringComparison.OrdinalIgnoreCase))
            {
                statusName = fieldValue.Name;
            }
            else if (string.Equals(fieldName, "Priority", StringComparison.OrdinalIgnoreCase))
            {
                priority = fieldValue.Name;
            }
        }

        return (statusName, priority);
    }

    private static bool MatchesStatusOption(TaskStatus status, string? statusName)
    {
        return statusName is not null
            && StatusOptionNames[status].Any(name => string.Equals(name, statusName, StringComparison.OrdinalIgnoreCase));
    }

    private async Task<string> GetViewerLoginAsync(CancellationToken cancellationToken)
    {
        var data = await ExecuteAsync<ViewerResponseDto>(ViewerLoginQuery, variables: null, cancellationToken);
        return data.Viewer?.Login
            ?? throw new InvalidOperationException("GitHub GraphQL response did not include the authenticated viewer's login.");
    }

    private async Task<List<ProjectItemNodeDto>> FetchProjectItemsAsync(CancellationToken cancellationToken)
    {
        var variables = new { login = _options.OwnerLogin, number = _options.ProjectNumber };

        var userData = await TryExecuteAsync<ProjectItemsResponseDto>(UserAssignedItemsQuery, variables, cancellationToken);
        if (userData?.User?.ProjectV2 is { } userProject)
        {
            return userProject.Items.Nodes;
        }

        var orgData = await TryExecuteAsync<ProjectItemsResponseDto>(OrganizationAssignedItemsQuery, variables, cancellationToken);
        if (orgData?.Organization?.ProjectV2 is { } orgProject)
        {
            return orgProject.Items.Nodes;
        }

        throw new InvalidOperationException(
            $"GitHub project #{_options.ProjectNumber} not found for owner '{_options.OwnerLogin}' (checked both user and organization).");
    }

    private async Task<ProjectV2Dto> FetchProjectWithFieldsAsync(CancellationToken cancellationToken)
    {
        var variables = new { login = _options.OwnerLogin, number = _options.ProjectNumber };

        var userData = await TryExecuteAsync<ProjectSchemaResponseDto>(UserSchemaQuery, variables, cancellationToken);
        if (userData?.User?.ProjectV2 is { } userProject)
        {
            return userProject;
        }

        var orgData = await TryExecuteAsync<ProjectSchemaResponseDto>(OrganizationSchemaQuery, variables, cancellationToken);
        if (orgData?.Organization?.ProjectV2 is { } orgProject)
        {
            return orgProject;
        }

        throw new InvalidOperationException(
            $"GitHub project #{_options.ProjectNumber} not found for owner '{_options.OwnerLogin}' (checked both user and organization).");
    }

    private static List<ProviderFieldDefinition> MapFields(List<FieldNodeDto> nodes)
    {
        var fields = new List<ProviderFieldDefinition>();

        foreach (var node in nodes)
        {
            var type = MapDataType(node.DataType);
            if (type is null || node.Id is null || node.Name is null)
            {
                // Unsupported field kind (Title, Assignees, Labels, Iteration, etc.) - not
                // exposed in the dynamic schema yet.
                continue;
            }

            var options = node.Options?.Select(o => new ProviderFieldOption(o.Id, o.Name)).ToArray()
                ?? [];

            // GitHub's API doesn't expose a "required" flag for custom fields.
            fields.Add(new ProviderFieldDefinition(node.Id, node.Name, type.Value, Required: false, options));
        }

        return fields;
    }

    private static ProviderFieldType? MapDataType(string? dataType) => dataType switch
    {
        "TEXT" => ProviderFieldType.Text,
        "NUMBER" => ProviderFieldType.Number,
        "DATE" => ProviderFieldType.Date,
        "SINGLE_SELECT" => ProviderFieldType.SingleSelect,
        _ => null,
    };

    private static object BuildFieldValuePayload(ProviderFieldType type, string rawValue) => type switch
    {
        ProviderFieldType.Text => new { text = rawValue },
        ProviderFieldType.Number => new { number = double.Parse(rawValue, CultureInfo.InvariantCulture) },
        ProviderFieldType.Date => new { date = rawValue },
        ProviderFieldType.SingleSelect => new { singleSelectOptionId = rawValue },
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, null),
    };

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

    /// <summary>
    /// Executes a query and ignores GraphQL-level errors, returning whatever data came back (or
    /// null). Used for the user/organization schema fallback, where an error on one branch (e.g.
    /// "not an Organization") is an expected, tolerated outcome, not a failure.
    /// </summary>
    private async Task<TData?> TryExecuteAsync<TData>(string query, object? variables, CancellationToken cancellationToken)
    {
        var envelope = await SendAsync<TData>(query, variables, cancellationToken);
        return envelope is null ? default : envelope.Data;
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
