namespace TaskEngine.Infrastructure.Providers.GitHub;

internal sealed record GraphQlRequestBody(string Query, object? Variables);

internal sealed class GraphQlEnvelope<TData>
{
    public TData? Data { get; set; }

    public List<GraphQlErrorDto>? Errors { get; set; }
}

internal sealed class GraphQlErrorDto
{
    public string Message { get; set; } = "";
}

/// <summary>Marker for mutations whose response payload we don't need to read.</summary>
internal sealed class EmptyDataDto
{
}

internal sealed class ProjectSchemaResponseDto
{
    public OwnerProjectDto? User { get; set; }

    public OwnerProjectDto? Organization { get; set; }
}

internal sealed class OwnerProjectDto
{
    public ProjectV2Dto? ProjectV2 { get; set; }
}

internal sealed class ProjectV2Dto
{
    public string Id { get; set; } = "";

    public FieldsConnectionDto Fields { get; set; } = new();
}

internal sealed class FieldsConnectionDto
{
    public List<FieldNodeDto> Nodes { get; set; } = [];
}

/// <summary>
/// Flat shape covering every GitHub Projects v2 field kind: <c>ProjectV2FieldCommon</c>
/// (id/name/dataType) applies to all of them, <c>Options</c> is only present when the node is
/// actually a <c>ProjectV2SingleSelectField</c> (GraphQL simply omits fields that don't apply).
/// </summary>
internal sealed class FieldNodeDto
{
    public string? Id { get; set; }

    public string? Name { get; set; }

    public string? DataType { get; set; }

    public List<OptionNodeDto>? Options { get; set; }
}

internal sealed class OptionNodeDto
{
    public string Id { get; set; } = "";

    public string Name { get; set; } = "";
}

internal sealed class AddDraftIssueResponseDto
{
    public AddDraftIssuePayloadDto? AddProjectV2DraftIssue { get; set; }
}

internal sealed class AddDraftIssuePayloadDto
{
    public ProjectItemDto? ProjectItem { get; set; }
}

internal sealed class ProjectItemDto
{
    public string Id { get; set; } = "";
}

internal sealed class ViewerResponseDto
{
    public ViewerDto? Viewer { get; set; }
}

internal sealed class ViewerDto
{
    public string Login { get; set; } = "";
}

internal sealed class ProjectItemsResponseDto
{
    public OwnerProjectItemsDto? User { get; set; }

    public OwnerProjectItemsDto? Organization { get; set; }
}

internal sealed class OwnerProjectItemsDto
{
    public ProjectV2ItemsDto? ProjectV2 { get; set; }
}

internal sealed class ProjectV2ItemsDto
{
    public ItemsConnectionDto Items { get; set; } = new();
}

internal sealed class ItemsConnectionDto
{
    public List<ProjectItemNodeDto> Nodes { get; set; } = [];
}

internal sealed class ProjectItemNodeDto
{
    public string Id { get; set; } = "";

    public FieldValuesConnectionDto FieldValues { get; set; } = new();

    public ProjectItemContentDto? Content { get; set; }
}

internal sealed class FieldValuesConnectionDto
{
    public List<FieldValueNodeDto> Nodes { get; set; } = [];
}

/// <summary>
/// Only covers <c>ProjectV2ItemFieldSingleSelectValue</c> (Status/Priority are both single-select
/// in this codebase's usage) - GraphQL omits fields that don't match the requested inline
/// fragment, same reasoning as <see cref="FieldNodeDto"/>.
/// </summary>
internal sealed class FieldValueNodeDto
{
    public string? Name { get; set; }

    public FieldRefDto? Field { get; set; }
}

internal sealed class FieldRefDto
{
    public string? Name { get; set; }
}

/// <summary>
/// Covers both <c>Issue</c> and <c>DraftIssue</c> project item content kinds - the fields queried
/// (title/body/createdAt/assignees) are shared between them, only <c>url</c> is Issue-only (draft
/// issues have no page of their own), so it's left null for draft issues rather than omitted.
/// </summary>
internal sealed class ProjectItemContentDto
{
    public string? Title { get; set; }

    public string? Body { get; set; }

    public DateTimeOffset? CreatedAt { get; set; }

    public string? Url { get; set; }

    public AssigneesConnectionDto? Assignees { get; set; }
}

internal sealed class AssigneesConnectionDto
{
    public List<AssigneeDto> Nodes { get; set; } = [];
}

internal sealed class AssigneeDto
{
    public string Login { get; set; } = "";
}
