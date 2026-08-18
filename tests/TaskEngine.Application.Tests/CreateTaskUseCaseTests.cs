using TaskEngine.Application.Providers;
using TaskEngine.Application.Tasks;
using TaskEngine.Domain.Entities;
using TaskStatus = TaskEngine.Domain.Entities.TaskStatus;

namespace TaskEngine.Application.Tests;

public class CreateTaskUseCaseTests
{
    [Fact]
    public async Task ExecuteAsync_WithValidTitle_PersistsTaskAndReturnsDto()
    {
        var repository = new FakeTaskRepository();
        var providerClientFactory = new FakeProviderClientFactory();
        var useCase = new CreateTaskUseCase(repository, providerClientFactory);
        var request = new CreateTaskRequest("Write report", "Quarterly summary");

        TaskDto result = await useCase.ExecuteAsync(request);

        TaskItem persisted = Assert.Single(repository.Tasks);
        Assert.Equal("Write report", persisted.Title);
        Assert.Equal("Quarterly summary", persisted.Description);
        Assert.Equal(TaskStatus.ToDo, persisted.Status);

        Assert.Equal(persisted.Id, result.Id);
        Assert.Equal("Write report", result.Title);
        Assert.Equal("Quarterly summary", result.Description);
        Assert.Equal(TaskStatus.ToDo.ToString(), result.Status);
        Assert.Equal(persisted.CreatedAt, result.CreatedAt);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public async Task ExecuteAsync_WithInvalidTitle_ThrowsAndDoesNotPersist(string? title)
    {
        var repository = new FakeTaskRepository();
        var providerClientFactory = new FakeProviderClientFactory();
        var useCase = new CreateTaskUseCase(repository, providerClientFactory);
        var request = new CreateTaskRequest(title!, "Some description");

        await Assert.ThrowsAsync<ArgumentException>(() => useCase.ExecuteAsync(request));

        Assert.Empty(repository.Tasks);
    }

    [Fact]
    public async Task ExecuteAsync_WithoutProviderId_DoesNotCallProviderClientFactory()
    {
        var repository = new FakeTaskRepository();
        var providerClientFactory = new FakeProviderClientFactory();
        var useCase = new CreateTaskUseCase(repository, providerClientFactory);
        var request = new CreateTaskRequest("Write report", "Quarterly summary");

        TaskDto result = await useCase.ExecuteAsync(request);

        Assert.Null(providerClientFactory.LastRequestedProviderId);
        Assert.Null(result.ProviderTaskId);
        Assert.Null(Assert.Single(repository.Tasks).ProviderTaskId);
    }

    [Fact]
    public async Task ExecuteAsync_WithProviderId_CreatesTaskOnProviderAndPersistsReference()
    {
        var repository = new FakeTaskRepository();
        var providerClientFactory = new FakeProviderClientFactory();
        providerClientFactory.ClientToReturn.ReferenceToReturn = new ProviderTaskReference("github", "gh-42", Url: null);
        var useCase = new CreateTaskUseCase(repository, providerClientFactory);
        var fieldValues = new Dictionary<string, string> { ["status"] = "todo" };
        var request = new CreateTaskRequest("Write report", "Quarterly summary", ProviderId: "github", ProviderFieldValues: fieldValues);

        TaskDto result = await useCase.ExecuteAsync(request);

        Assert.Equal("github", providerClientFactory.LastRequestedProviderId);
        Assert.Same(fieldValues, providerClientFactory.ClientToReturn.LastFieldValues);
        Assert.Equal("Write report", providerClientFactory.ClientToReturn.LastCreatedTask?.Title);

        TaskItem persisted = Assert.Single(repository.Tasks);
        Assert.Equal("github", persisted.ProviderId);
        Assert.Equal("gh-42", persisted.ProviderTaskId);
        Assert.Equal("gh-42", result.ProviderTaskId);
    }
}
