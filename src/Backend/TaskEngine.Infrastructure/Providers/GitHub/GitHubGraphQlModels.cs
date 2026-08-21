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

internal sealed class SearchResponseDto
{
    public SearchConnectionDto? Search { get; set; }
}

internal sealed class SearchConnectionDto
{
    public List<IssueNodeDto> Nodes { get; set; } = [];

    public PageInfoDto PageInfo { get; set; } = new();
}

internal sealed class PageInfoDto
{
    public bool HasNextPage { get; set; }

    public string? EndCursor { get; set; }
}

/// <summary>
/// <c>search(type: ISSUE)</c> nodes are typed as <c>SearchResultItem</c> - only the <c>... on
/// Issue</c> inline fragment requested in the query actually resolves, but GraphQL still flattens
/// its fields into this same flat JSON object per node (same behavior already relied on for
/// GitHub Projects v2 union/interface fields elsewhere in this codebase).
/// </summary>
internal sealed class IssueNodeDto
{
    public string? Id { get; set; }

    public string? Title { get; set; }

    public string? Body { get; set; }

    public DateTimeOffset? CreatedAt { get; set; }

    public string? Url { get; set; }

    /// <summary>Raw GraphQL <c>IssueState</c> value: <c>"OPEN"</c> or <c>"CLOSED"</c>.</summary>
    public string? State { get; set; }

    public LabelsConnectionDto? Labels { get; set; }
}

internal sealed class LabelsConnectionDto
{
    public List<LabelNodeDto> Nodes { get; set; } = [];
}

internal sealed class LabelNodeDto
{
    public string Name { get; set; } = "";
}
