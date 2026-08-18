namespace TaskEngine.Application.Providers;

/// <summary>
/// Identifies a task as tracked by an external provider (e.g. GitHub, Jira, ClickUp).
/// </summary>
public sealed record ProviderTaskReference(string ProviderId, string ExternalId, string? Url);
