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
