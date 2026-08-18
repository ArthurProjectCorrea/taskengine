namespace TaskEngine.Application.Providers;

/// <summary>
/// The set of fields a specific provider (and connected project/board) expects for task
/// creation. Fetched after connecting a provider so the task creation UI can render fields
/// dynamically instead of a fixed form.
/// </summary>
public sealed record ProviderTaskSchema(string ProviderId, IReadOnlyList<ProviderFieldDefinition> Fields);
