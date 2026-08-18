namespace TaskEngine.Application.Providers;

/// <summary>
/// One selectable option of a <see cref="ProviderFieldType.SingleSelect"/> field (e.g. a Status
/// column or a Priority value configured on the provider's side).
/// </summary>
public sealed record ProviderFieldOption(string Id, string Name);
