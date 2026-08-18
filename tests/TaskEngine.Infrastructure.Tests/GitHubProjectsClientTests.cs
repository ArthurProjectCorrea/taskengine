using TaskEngine.Application.Providers;
using TaskEngine.Domain.Entities;
using TaskEngine.Infrastructure.Providers.GitHub;

using TaskStatus = TaskEngine.Domain.Entities.TaskStatus;

namespace TaskEngine.Infrastructure.Tests;

public class GitHubProjectsClientTests
{
    private const string SchemaResponse = """
        {
          "data": {
            "user": {
              "projectV2": {
                "id": "PVT_project1",
                "fields": {
                  "nodes": [
                    { "id": "F_title", "name": "Title", "dataType": "TITLE" },
                    {
                      "id": "F_status",
                      "name": "Status",
                      "dataType": "SINGLE_SELECT",
                      "options": [
                        { "id": "OPT_todo", "name": "Todo" },
                        { "id": "OPT_progress", "name": "In Progress" },
                        { "id": "OPT_done", "name": "Done" }
                      ]
                    },
                    { "id": "F_estimate", "name": "Estimate", "dataType": "NUMBER" }
                  ]
                }
              }
            },
            "organization": { "projectV2": null }
          }
        }
        """;

    private const string AddDraftIssueResponse = """
        { "data": { "addProjectV2DraftIssue": { "projectItem": { "id": "PVTI_item1" } } } }
        """;

    private const string UpdateFieldValueResponse = """
        { "data": { "updateProjectV2ItemFieldValue": { "projectV2Item": { "id": "PVTI_item1" } } } }
        """;

    private static GitHubProjectsClient CreateClient(FakeHttpMessageHandler handler) =>
        new(new HttpClient(handler), new GitHubProjectsOptions("test-token", "octocat", 1));

    [Fact]
    public async Task GetTaskSchemaAsync_MapsSupportedFieldsAndFiltersUnsupportedOnes()
    {
        var handler = new FakeHttpMessageHandler(SchemaResponse);
        var client = CreateClient(handler);

        ProviderTaskSchema schema = await client.GetTaskSchemaAsync(CancellationToken.None);

        Assert.Equal("github", schema.ProviderId);
        Assert.Equal(2, schema.Fields.Count); // "Title" (TITLE) is filtered out - not a supported dynamic field kind.

        var status = Assert.Single(schema.Fields, f => f.Label == "Status");
        Assert.Equal(ProviderFieldType.SingleSelect, status.Type);
        Assert.Equal(3, status.Options.Count);
        Assert.Contains(status.Options, o => o is { Name: "Done", Id: "OPT_done" });

        var estimate = Assert.Single(schema.Fields, f => f.Label == "Estimate");
        Assert.Equal(ProviderFieldType.Number, estimate.Type);
        Assert.Empty(estimate.Options);
    }

    [Fact]
    public async Task CreateTaskAsync_WithoutFieldValues_OnlyFetchesSchemaAndCreatesDraftIssue()
    {
        var handler = new FakeHttpMessageHandler(SchemaResponse, AddDraftIssueResponse);
        var client = CreateClient(handler);
        var task = TaskItem.Create("Write report", "Quarterly summary");

        ProviderTaskReference reference = await client.CreateTaskAsync(task, fieldValues: null, CancellationToken.None);

        Assert.Equal("github", reference.ProviderId);
        Assert.Equal("PVTI_item1", reference.ExternalId);
        Assert.Equal(2, handler.CapturedRequestBodies.Count);
        Assert.Contains("Write report", handler.CapturedRequestBodies[1]);
    }

    [Fact]
    public async Task CreateTaskAsync_WithFieldValues_SendsOneMutationPerRecognizedField()
    {
        var handler = new FakeHttpMessageHandler(
            SchemaResponse, AddDraftIssueResponse, UpdateFieldValueResponse, UpdateFieldValueResponse);
        var client = CreateClient(handler);
        var task = TaskItem.Create("Write report");

        var fieldValues = new Dictionary<string, string>
        {
            ["F_status"] = "OPT_done",
            ["F_estimate"] = "3.5",
            ["F_unknown"] = "ignored - no matching field in schema",
        };

        await client.CreateTaskAsync(task, fieldValues, CancellationToken.None);

        // schema + create + 2 recognized field updates ("F_unknown" is skipped, not a 3rd mutation).
        Assert.Equal(4, handler.CapturedRequestBodies.Count);
        Assert.Contains("singleSelectOptionId", handler.CapturedRequestBodies[2] + handler.CapturedRequestBodies[3]);
    }

    [Fact]
    public async Task UpdateStatusAsync_UsesTheOptionMatchingTheDomainStatus()
    {
        var handler = new FakeHttpMessageHandler(SchemaResponse, UpdateFieldValueResponse);
        var client = CreateClient(handler);
        var reference = new ProviderTaskReference("github", "PVTI_item1", null);

        await client.UpdateStatusAsync(reference, TaskStatus.Done, CancellationToken.None);

        Assert.Equal(2, handler.CapturedRequestBodies.Count);
        Assert.Contains("OPT_done", handler.CapturedRequestBodies[1]);
    }

    [Fact]
    public async Task UpdateStatusAsync_ThrowsWhenProjectHasNoStatusField()
    {
        const string schemaWithoutStatus = """
            {
              "data": {
                "user": {
                  "projectV2": {
                    "id": "PVT_project1",
                    "fields": { "nodes": [ { "id": "F_title", "name": "Title", "dataType": "TITLE" } ] }
                  }
                },
                "organization": { "projectV2": null }
              }
            }
            """;
        var handler = new FakeHttpMessageHandler(schemaWithoutStatus);
        var client = CreateClient(handler);
        var reference = new ProviderTaskReference("github", "PVTI_item1", null);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => client.UpdateStatusAsync(reference, TaskStatus.Done, CancellationToken.None));
    }

    [Fact]
    public async Task CreateTaskAsync_ThrowsOnGraphQlErrorsFromMutation()
    {
        const string mutationErrorResponse = """
            { "data": null, "errors": [ { "message": "Resource not accessible by personal access token." } ] }
            """;
        var handler = new FakeHttpMessageHandler(SchemaResponse, mutationErrorResponse);
        var client = CreateClient(handler);
        var task = TaskItem.Create("Write report");

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => client.CreateTaskAsync(task, fieldValues: null, CancellationToken.None));
        Assert.Contains("Resource not accessible", exception.Message);
    }

    [Fact]
    public async Task GetTaskSchemaAsync_TriesOrganizationWhenUserLookupFindsNoProject()
    {
        const string userNullResponse = """{ "data": { "user": { "projectV2": null } } }""";
        const string orgResponse = """
            {
              "data": {
                "organization": {
                  "projectV2": {
                    "id": "PVT_orgproject",
                    "fields": { "nodes": [ { "id": "F_status", "name": "Status", "dataType": "SINGLE_SELECT", "options": [] } ] }
                  }
                }
              }
            }
            """;
        var handler = new FakeHttpMessageHandler(userNullResponse, orgResponse);
        var client = CreateClient(handler);

        var schema = await client.GetTaskSchemaAsync(CancellationToken.None);

        Assert.Equal(2, handler.CapturedRequestBodies.Count);
        Assert.Single(schema.Fields);
    }

    [Fact]
    public async Task GetTaskSchemaAsync_ThrowsWhenProjectNotFoundForEitherOwnerKind()
    {
        const string userNullResponse = """{ "data": { "user": { "projectV2": null } } }""";
        const string orgNullResponse = """{ "data": { "organization": { "projectV2": null } } }""";
        var handler = new FakeHttpMessageHandler(userNullResponse, orgNullResponse);
        var client = CreateClient(handler);

        await Assert.ThrowsAsync<InvalidOperationException>(() => client.GetTaskSchemaAsync(CancellationToken.None));
        Assert.Equal(2, handler.CapturedRequestBodies.Count);
    }
}
