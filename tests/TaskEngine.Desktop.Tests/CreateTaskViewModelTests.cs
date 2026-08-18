using System.Text.Json;
using TaskEngine.Application.Providers;
using TaskEngine.Application.Tasks;
using TaskEngine.Desktop.ViewModels;

namespace TaskEngine.Desktop.Tests;

public class CreateTaskViewModelTests
{
    private static readonly ProviderTaskSchema SampleSchema = new(
        "github",
        [
            new ProviderFieldDefinition(
                "status",
                "Status",
                ProviderFieldType.SingleSelect,
                false,
                [new ProviderFieldOption("opt-in-progress", "In Progress"), new ProviderFieldOption("opt-done", "Done")]),
            new ProviderFieldDefinition("priority", "Priority", ProviderFieldType.Text, false, []),
            new ProviderFieldDefinition("estimate", "Estimate", ProviderFieldType.Number, false, []),
        ]);

    [Fact]
    public async Task InitializeAsync_WhenSchemaIsCached_UsesCache_AndDoesNotCallProviderClientFactory()
    {
        var appSettingsStore = new FakeAppSettingsStore();
        await appSettingsStore.SetAsync("provider:connected", "github", CancellationToken.None);
        await appSettingsStore.SetAsync("provider:github:schema", JsonSerializer.Serialize(SampleSchema), CancellationToken.None);

        var clientFactory = new FakeProviderClientFactory();
        var useCase = new CreateTaskUseCase(new FakeTaskRepository(), clientFactory);
        var viewModel = new CreateTaskViewModel(useCase, clientFactory, appSettingsStore);

        await viewModel.InitializeAsync(CancellationToken.None);

        Assert.Equal(CreateTaskState.Ready, viewModel.State);
        Assert.Equal("github", viewModel.ConnectedProviderId);
        Assert.Equal(3, viewModel.Fields.Count);
        Assert.Equal(0, clientFactory.CreateCallCount);
    }

    [Fact]
    public async Task InitializeAsync_WhenSchemaIsNotCached_FetchesLiveViaProviderClientFactory()
    {
        var appSettingsStore = new FakeAppSettingsStore();
        await appSettingsStore.SetAsync("provider:connected", "github", CancellationToken.None);

        var client = new FakeTaskProviderClient("github", SampleSchema);
        var clientFactory = new FakeProviderClientFactory(client);
        var useCase = new CreateTaskUseCase(new FakeTaskRepository(), clientFactory);
        var viewModel = new CreateTaskViewModel(useCase, clientFactory, appSettingsStore);

        await viewModel.InitializeAsync(CancellationToken.None);

        Assert.Equal(CreateTaskState.Ready, viewModel.State);
        Assert.Equal(3, viewModel.Fields.Count);
        Assert.Equal(1, clientFactory.CreateCallCount);
        Assert.Equal(1, client.GetTaskSchemaCallCount);
    }

    [Fact]
    public async Task InitializeAsync_WhenNoProviderIsConnected_TransitionsToError()
    {
        var appSettingsStore = new FakeAppSettingsStore();
        var clientFactory = new FakeProviderClientFactory();
        var useCase = new CreateTaskUseCase(new FakeTaskRepository(), clientFactory);
        var viewModel = new CreateTaskViewModel(useCase, clientFactory, appSettingsStore);

        await viewModel.InitializeAsync(CancellationToken.None);

        Assert.Equal(CreateTaskState.Error, viewModel.State);
        Assert.NotNull(viewModel.ErrorMessage);
        Assert.Null(viewModel.ConnectedProviderId);
    }

    [Fact]
    public async Task SubmitAsync_OnSuccess_SendsSingleSelectOptionId_NotDisplayName_AndSkipsEmptyFields()
    {
        var appSettingsStore = new FakeAppSettingsStore();
        await appSettingsStore.SetAsync("provider:connected", "github", CancellationToken.None);
        await appSettingsStore.SetAsync("provider:github:schema", JsonSerializer.Serialize(SampleSchema), CancellationToken.None);

        var createTaskResult = new ProviderTaskReference("github", "gh-123", "https://github.com/x/y/issues/123");
        var client = new FakeTaskProviderClient("github", SampleSchema, createTaskResult);
        var clientFactory = new FakeProviderClientFactory(client);
        var useCase = new CreateTaskUseCase(new FakeTaskRepository(), clientFactory);
        var viewModel = new CreateTaskViewModel(useCase, clientFactory, appSettingsStore);

        await viewModel.InitializeAsync(CancellationToken.None);

        viewModel.Title = "Write the release notes";

        var statusField = Assert.Single(viewModel.Fields, f => f.Key == "status");
        statusField.SelectedOption = statusField.Options.Single(o => o.Id == "opt-done");

        var priorityField = Assert.Single(viewModel.Fields, f => f.Key == "priority");
        priorityField.Value = "High";

        // "estimate" is intentionally left empty - must be skipped, not sent as "".

        await viewModel.SubmitCommand.ExecuteAsync(null);

        Assert.Equal(CreateTaskState.Success, viewModel.State);
        Assert.Null(viewModel.ErrorMessage);
        Assert.NotNull(viewModel.CreatedTask);
        Assert.Equal(1, client.CreateTaskCallCount);

        var sentFieldValues = client.LastCreateTaskFieldValues;
        Assert.NotNull(sentFieldValues);
        Assert.Equal("opt-done", sentFieldValues!["status"]);
        Assert.Equal("High", sentFieldValues["priority"]);
        Assert.False(sentFieldValues.ContainsKey("estimate"));
    }

    [Fact]
    public async Task SubmitAsync_WhenProviderCreateFails_TransitionsToError_WithMessage()
    {
        var appSettingsStore = new FakeAppSettingsStore();
        await appSettingsStore.SetAsync("provider:connected", "github", CancellationToken.None);
        await appSettingsStore.SetAsync("provider:github:schema", JsonSerializer.Serialize(SampleSchema), CancellationToken.None);

        var client = new FakeTaskProviderClient("github", new InvalidOperationException("GitHub project not configured."));
        var clientFactory = new FakeProviderClientFactory(client);
        var useCase = new CreateTaskUseCase(new FakeTaskRepository(), clientFactory);
        var viewModel = new CreateTaskViewModel(useCase, clientFactory, appSettingsStore);

        await viewModel.InitializeAsync(CancellationToken.None);
        viewModel.Title = "Write the release notes";

        await viewModel.SubmitCommand.ExecuteAsync(null);

        Assert.Equal(CreateTaskState.Error, viewModel.State);
        Assert.Equal("GitHub project not configured.", viewModel.ErrorMessage);
        Assert.Null(viewModel.CreatedTask);
    }
}
