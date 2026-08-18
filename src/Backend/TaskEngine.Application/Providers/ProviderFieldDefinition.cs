namespace TaskEngine.Application.Providers;

/// <summary>
/// One field a provider expects when creating/updating a task (e.g. Status, Priority,
/// Estimate). <see cref="Options"/> is only populated for <see cref="ProviderFieldType.SingleSelect"/>.
/// </summary>
public sealed record ProviderFieldDefinition(
    string Key,
    string Label,
    ProviderFieldType Type,
    bool Required,
    IReadOnlyList<ProviderFieldOption> Options);
