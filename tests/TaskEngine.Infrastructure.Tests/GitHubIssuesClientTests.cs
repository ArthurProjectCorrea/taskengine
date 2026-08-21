using TaskEngine.Application.Providers;
using TaskEngine.Infrastructure.Providers.GitHub;

using TaskStatus = TaskEngine.Domain.Entities.TaskStatus;

namespace TaskEngine.Infrastructure.Tests;

public class GitHubIssuesClientTests
{
    private static GitHubIssuesClient CreateClient(FakeHttpMessageHandler handler) =>
        CreateClient(handler, new FakeAppSettingsStore());

    private static GitHubIssuesClient CreateClient(FakeHttpMessageHandler handler, FakeAppSettingsStore appSettingsStore) =>
        new(new HttpClient(handler), new GitHubIssuesOptions("test-token"), appSettingsStore);

    private static string SearchResponse(bool hasNextPage = false, string? endCursor = null) => $$"""
        {
          "data": {
            "search": {
              "pageInfo": { "hasNextPage": {{(hasNextPage ? "true" : "false")}}, "endCursor": {{(endCursor is null ? "null" : $"\"{endCursor}\"")}} },
              "nodes": [
                {
                  "id": "I_todo",
                  "title": "Fix the bug",
                  "body": "Details",
                  "createdAt": "2026-01-01T00:00:00Z",
                  "url": "https://github.com/octocat/repo/issues/1",
                  "state": "OPEN",
                  "labels": { "nodes": [ { "name": "priority: high" } ] }
                },
                {
                  "id": "I_done",
                  "title": "Write docs",
                  "body": null,
                  "createdAt": "2026-01-02T00:00:00Z",
                  "url": "https://github.com/octocat/repo/issues/2",
                  "state": "CLOSED",
                  "labels": { "nodes": [] }
                }
              ]
            }
          }
        }
        """;

    [Fact]
    public async Task ListAssignedTasksAsync_MapsOpenAndClosedIssues()
    {
        var handler = new FakeHttpMessageHandler(SearchResponse());
        var client = CreateClient(handler);

        IReadOnlyList<ProviderTaskSummary> tasks = await client.ListAssignedTasksAsync(CancellationToken.None);

        Assert.Equal(2, tasks.Count);

        var todo = Assert.Single(tasks, t => t.ExternalId == "I_todo");
        Assert.Equal("Fix the bug", todo.Title);
        Assert.Equal("Details", todo.Description);
        Assert.Equal("open", todo.StatusName);
        Assert.False(todo.IsInProgress);
        Assert.False(todo.IsDone);
        Assert.Equal("high", todo.Priority);
        Assert.Equal(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero), todo.CreatedAt);
        Assert.Equal("https://github.com/octocat/repo/issues/1", todo.Url);

        var done = Assert.Single(tasks, t => t.ExternalId == "I_done");
        Assert.Equal("closed", done.StatusName);
        Assert.False(done.IsInProgress);
        Assert.True(done.IsDone);
        Assert.Null(done.Priority);
    }

    [Fact]
    public async Task ListAssignedTasksAsync_SendsTheAssigneeAtMeSearchQuery()
    {
        var handler = new FakeHttpMessageHandler(SearchResponse());
        var client = CreateClient(handler);

        await client.ListAssignedTasksAsync(CancellationToken.None);

        Assert.Single(handler.CapturedRequestBodies);
        Assert.Contains("is:issue assignee:@me archived:false", handler.CapturedRequestBodies[0]);
    }

    [Fact]
    public async Task ListAssignedTasksAsync_PagesUntilHasNextPageIsFalse()
    {
        var handler = new FakeHttpMessageHandler(
            SearchResponse(hasNextPage: true, endCursor: "cursor1"),
            SearchResponse());
        var client = CreateClient(handler);

        IReadOnlyList<ProviderTaskSummary> tasks = await client.ListAssignedTasksAsync(CancellationToken.None);

        Assert.Equal(2, handler.CapturedRequestBodies.Count);
        Assert.Contains("cursor1", handler.CapturedRequestBodies[1]);
        Assert.Equal(4, tasks.Count); // two pages of two issues each, same ids repeated in this fixture is fine - not deduplicated.
    }

    [Theory]
    [InlineData("P1", "P1")]
    [InlineData("p2", "P2")]
    [InlineData("critical", "critical")]
    [InlineData("Medium", "medium")]
    [InlineData("unrecognized-label", null)]
    public async Task ListAssignedTasksAsync_RecognizesCommonPriorityLabelPatterns(string label, string? expectedPriority)
    {
        var response = $$"""
            {
              "data": {
                "search": {
                  "pageInfo": { "hasNextPage": false, "endCursor": null },
                  "nodes": [
                    {
                      "id": "I_1",
                      "title": "Task",
                      "body": null,
                      "createdAt": "2026-01-01T00:00:00Z",
                      "url": null,
                      "state": "OPEN",
                      "labels": { "nodes": [ { "name": "{{label}}" } ] }
                    }
                  ]
                }
              }
            }
            """;
        var handler = new FakeHttpMessageHandler(response);
        var client = CreateClient(handler);

        IReadOnlyList<ProviderTaskSummary> tasks = await client.ListAssignedTasksAsync(CancellationToken.None);

        Assert.Equal(expectedPriority, Assert.Single(tasks).Priority);
    }

    [Fact]
    public async Task ListAssignedTasksAsync_ThrowsOnGraphQlErrors()
    {
        const string errorResponse = """
            { "data": null, "errors": [ { "message": "Resource not accessible by personal access token." } ] }
            """;
        var handler = new FakeHttpMessageHandler(errorResponse);
        var client = CreateClient(handler);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => client.ListAssignedTasksAsync(CancellationToken.None));
        Assert.Contains("Resource not accessible", exception.Message);
    }

    [Fact]
    public async Task ListAssignedTasksAsync_On401_FreezesTheProviderAndThrowsProviderFrozenException()
    {
        var handler = new FakeHttpMessageHandler(SearchResponse()) { StatusCode = System.Net.HttpStatusCode.Unauthorized };
        var appSettingsStore = new FakeAppSettingsStore();
        var client = CreateClient(handler, appSettingsStore);

        var exception = await Assert.ThrowsAsync<ProviderFrozenException>(
            () => client.ListAssignedTasksAsync(CancellationToken.None));

        Assert.Equal("github", exception.ProviderId);
        Assert.Equal("true", appSettingsStore.Values[ProviderSettingsKeys.Frozen("github")]);
    }

    private const string CloseIssueResponse = """
        { "data": { "closeIssue": { "issue": { "id": "I_1", "state": "CLOSED" } } } }
        """;

    private const string ReopenIssueResponse = """
        { "data": { "reopenIssue": { "issue": { "id": "I_1", "state": "OPEN" } } } }
        """;

    [Theory]
    [InlineData(TaskStatus.Done)]
    [InlineData(TaskStatus.DonePendingSync)]
    public async Task UpdateStatusAsync_ForDoneStatuses_ClosesTheIssue(TaskStatus status)
    {
        var handler = new FakeHttpMessageHandler(CloseIssueResponse);
        var client = CreateClient(handler);
        var reference = new ProviderTaskReference("github", "I_1", null);

        await client.UpdateStatusAsync(reference, status, CancellationToken.None);

        Assert.Single(handler.CapturedRequestBodies);
        Assert.Contains("closeIssue", handler.CapturedRequestBodies[0]);
        Assert.Contains("I_1", handler.CapturedRequestBodies[0]);
    }

    [Theory]
    [InlineData(TaskStatus.ToDo)]
    [InlineData(TaskStatus.InProgress)]
    [InlineData(TaskStatus.Paused)]
    public async Task UpdateStatusAsync_ForNonDoneStatuses_ReopensTheIssue(TaskStatus status)
    {
        var handler = new FakeHttpMessageHandler(ReopenIssueResponse);
        var client = CreateClient(handler);
        var reference = new ProviderTaskReference("github", "I_1", null);

        await client.UpdateStatusAsync(reference, status, CancellationToken.None);

        Assert.Single(handler.CapturedRequestBodies);
        Assert.Contains("reopenIssue", handler.CapturedRequestBodies[0]);
    }

    private const string AddCommentResponse = """
        { "data": { "addComment": { "commentEdge": { "node": { "id": "IC_1" } } } } }
        """;

    [Fact]
    public async Task ReportCompletionAsync_ClosesTheIssueAndPostsAFormattedTimeComment()
    {
        var handler = new FakeHttpMessageHandler(CloseIssueResponse, AddCommentResponse);
        var client = CreateClient(handler);
        var reference = new ProviderTaskReference("github", "I_1", null);

        await client.ReportCompletionAsync(reference, TimeSpan.FromMinutes(150), CancellationToken.None);

        Assert.Equal(2, handler.CapturedRequestBodies.Count);
        Assert.Contains("closeIssue", handler.CapturedRequestBodies[0]);
        Assert.Contains("addComment", handler.CapturedRequestBodies[1]);
        Assert.Contains("Tempo investido (TaskEngine): 2h 30min", handler.CapturedRequestBodies[1]);
    }

    [Theory]
    [InlineData(0, 45, "45min")]
    [InlineData(3, 0, "3h")]
    [InlineData(1, 15, "1h 15min")]
    public async Task ReportCompletionAsync_FormatsDurationReadably(int hours, int minutes, string expectedFragment)
    {
        var handler = new FakeHttpMessageHandler(CloseIssueResponse, AddCommentResponse);
        var client = CreateClient(handler);
        var reference = new ProviderTaskReference("github", "I_1", null);

        await client.ReportCompletionAsync(reference, new TimeSpan(hours, minutes, 0), CancellationToken.None);

        Assert.Contains($"Tempo investido (TaskEngine): {expectedFragment}", handler.CapturedRequestBodies[1]);
    }
}
