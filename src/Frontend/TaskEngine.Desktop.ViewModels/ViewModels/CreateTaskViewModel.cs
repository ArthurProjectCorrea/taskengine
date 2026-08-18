using System.Collections.ObjectModel;
using System.Text.Json;
using TaskEngine.Application.Abstractions;
using TaskEngine.Application.Providers;
using TaskEngine.Application.Tasks;
using TaskEngine.Desktop.Mvvm;

namespace TaskEngine.Desktop.ViewModels;

public enum CreateTaskState
{
    Idle,
    LoadingSchema,
    Ready,
    Submitting,
    Success,
    Error,
}

/// <summary>
/// One dynamic field rendered by <c>CreateTaskPage</c>, built from a <see cref="ProviderFieldDefinition"/>
/// fetched from the provider's schema. <see cref="Value"/> is what actually gets sent to the
/// backend as the field value; for <see cref="ProviderFieldType.SingleSelect"/> fields it must be
/// the selected option's <see cref="ProviderFieldOption.Id"/>, never its display
/// <see cref="ProviderFieldOption.Name"/> (per <see cref="ITaskProviderClient.CreateTaskAsync"/>'s
/// documented contract). <see cref="SelectedOption"/>'s setter is what enforces that - the view
/// only ever binds <see cref="Value"/> (Text/Number/Date fields) or <see cref="SelectedOption"/>
/// (SingleSelect fields) and never has to know about the id-vs-name rule itself.
/// </summary>
public sealed class TaskFieldInputViewModel : ObservableObject
{
    private string? _value;
    private ProviderFieldOption? _selectedOption;

    public TaskFieldInputViewModel(ProviderFieldDefinition definition)
    {
        Key = definition.Key;
        Label = definition.Label;
        Type = definition.Type;
        Options = definition.Options;
    }

    public string Key { get; }

    public string Label { get; }

    /// <summary>Drives which control the view renders for this field (see <c>CreateTaskPage.xaml</c>'s template selector).</summary>
    public ProviderFieldType Type { get; }

    /// <summary>Only populated for <see cref="ProviderFieldType.SingleSelect"/> fields; empty otherwise.</summary>
    public IReadOnlyList<ProviderFieldOption> Options { get; }

    /// <summary>
    /// Value sent to the backend for this field. Text/Number/Date fields bind directly to this;
    /// SingleSelect fields should bind to <see cref="SelectedOption"/> instead and let its setter
    /// keep this in sync with the option's id.
    /// </summary>
    public string? Value
    {
        get => _value;
        set => SetProperty(ref _value, value);
    }

    /// <summary>Bound by the SingleSelect field template's Picker. Setting this updates <see cref="Value"/> to the option's Id (not its Name).</summary>
    public ProviderFieldOption? SelectedOption
    {
        get => _selectedOption;
        set
        {
            if (SetProperty(ref _selectedOption, value))
            {
                Value = value?.Id;
            }
        }
    }
}

/// <summary>
/// View model for the task creation screen (issue #26). Two-step flow on the same screen: once a
/// provider is connected (issue #20 - persisted under a single "provider:connected" key, so there
/// is at most one to resolve, not a list to choose from today), fetches that provider's field
/// schema (from the #20 best-effort cache, or live if not cached yet) and exposes it as a list of
/// <see cref="TaskFieldInputViewModel"/> for the view to render dynamically; then submits the fixed
/// Title/Description plus those dynamic field values via <see cref="CreateTaskUseCase"/>. Contains
/// presentation logic only - schema shape, provider client construction and persistence are all
/// delegated to the injected ports; no business rule (e.g. "Title is required") is duplicated here,
/// it's left to <see cref="CreateTaskUseCase"/>/<c>TaskItem.Create</c> to enforce and surface via
/// the same error path as any other failure.
/// </summary>
public sealed class CreateTaskViewModel : ObservableObject
{
    private const string ConnectedProviderSettingKey = "provider:connected";

    private readonly CreateTaskUseCase _createTaskUseCase;
    private readonly IProviderClientFactory _providerClientFactory;
    private readonly IAppSettingsStore _appSettingsStore;

    private CreateTaskState _state = CreateTaskState.Idle;
    private string? _errorMessage;
    private string? _connectedProviderId;
    private string _title = string.Empty;
    private string? _description;
    private TaskDto? _createdTask;

    public CreateTaskViewModel(
        CreateTaskUseCase createTaskUseCase,
        IProviderClientFactory providerClientFactory,
        IAppSettingsStore appSettingsStore)
    {
        _createTaskUseCase = createTaskUseCase;
        _providerClientFactory = providerClientFactory;
        _appSettingsStore = appSettingsStore;

        Fields = new ObservableCollection<TaskFieldInputViewModel>();
        SubmitCommand = new AsyncRelayCommand(_ => SubmitAsync(CancellationToken.None));
    }

    /// <summary>Dynamic fields rendered below the fixed Title/Description, one per <see cref="ProviderFieldDefinition"/> in the loaded schema.</summary>
    public ObservableCollection<TaskFieldInputViewModel> Fields { get; }

    public AsyncRelayCommand SubmitCommand { get; }

    public CreateTaskState State
    {
        get => _state;
        private set => SetProperty(ref _state, value);
    }

    public string? ErrorMessage
    {
        get => _errorMessage;
        private set => SetProperty(ref _errorMessage, value);
    }

    /// <summary>
    /// The provider connected via onboarding, read once by <see cref="InitializeAsync"/>. Modeled
    /// as a single value (not a selectable list) because <see cref="IAppSettingsStore"/> only ever
    /// persists one under the "provider:connected" key today (issue #20) - there is nothing to
    /// actually choose between yet, so a "step 1: pick a provider" UI would have no real data
    /// behind it.
    /// </summary>
    public string? ConnectedProviderId
    {
        get => _connectedProviderId;
        private set => SetProperty(ref _connectedProviderId, value);
    }

    public string Title
    {
        get => _title;
        set => SetProperty(ref _title, value);
    }

    public string? Description
    {
        get => _description;
        set => SetProperty(ref _description, value);
    }

    /// <summary>Populated once <see cref="SubmitAsync"/> succeeds; exposed mainly for testability/confirmation display.</summary>
    public TaskDto? CreatedTask
    {
        get => _createdTask;
        private set => SetProperty(ref _createdTask, value);
    }

    /// <summary>
    /// MAUI has no async constructor: <c>CreateTaskPage</c> calls this once it's loaded on screen
    /// to read the connected provider and fetch/render its field schema.
    /// </summary>
    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        State = CreateTaskState.LoadingSchema;
        ErrorMessage = null;

        try
        {
            var connectedProviderId = await _appSettingsStore.GetAsync(ConnectedProviderSettingKey, cancellationToken);
            if (connectedProviderId is null)
            {
                throw new InvalidOperationException("No provider is connected yet. Connect one first.");
            }

            ConnectedProviderId = connectedProviderId;

            var schema = await LoadSchemaAsync(connectedProviderId, cancellationToken);

            Fields.Clear();
            foreach (var field in schema.Fields)
            {
                Fields.Add(new TaskFieldInputViewModel(field));
            }

            State = CreateTaskState.Ready;
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            State = CreateTaskState.Error;
        }
    }

    /// <summary>
    /// Prefers the schema cached by onboarding (issue #20, best-effort - may be missing) over a
    /// live provider call, since it's already available with no extra I/O; falls back to
    /// <see cref="IProviderClientFactory"/> when there's nothing cached yet.
    /// </summary>
    private async Task<ProviderTaskSchema> LoadSchemaAsync(string providerId, CancellationToken cancellationToken)
    {
        var cachedSchemaJson = await _appSettingsStore.GetAsync(SchemaSettingKey(providerId), cancellationToken);
        if (cachedSchemaJson is not null)
        {
            var cachedSchema = JsonSerializer.Deserialize<ProviderTaskSchema>(cachedSchemaJson);
            if (cachedSchema is not null)
            {
                return cachedSchema;
            }
        }

        var client = await _providerClientFactory.CreateAsync(providerId, cancellationToken);
        return await client.GetTaskSchemaAsync(cancellationToken);
    }

    public async Task SubmitAsync(CancellationToken cancellationToken)
    {
        State = CreateTaskState.Submitting;
        ErrorMessage = null;

        try
        {
            // Skips empty/untouched fields rather than sending them as empty strings - none of
            // today's schema fields carry a reliable Required flag from GitHub, so there is no
            // sound way to distinguish "must fill this in" from "optional" here; the provider
            // itself is left to reject a genuinely required-but-missing field.
            var fieldValues = Fields
                .Where(field => !string.IsNullOrWhiteSpace(field.Value))
                .ToDictionary(field => field.Key, field => field.Value!);

            var request = new CreateTaskRequest(Title, Description, ConnectedProviderId, fieldValues);

            CreatedTask = await _createTaskUseCase.ExecuteAsync(request, cancellationToken);
            State = CreateTaskState.Success;
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            State = CreateTaskState.Error;
        }
    }

    private static string SchemaSettingKey(string providerId) => $"provider:{providerId}:schema";
}
