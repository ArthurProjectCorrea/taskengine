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
        var useCase = new CreateTaskUseCase(repository);
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
        var useCase = new CreateTaskUseCase(repository);
        var request = new CreateTaskRequest(title!, "Some description");

        await Assert.ThrowsAsync<ArgumentException>(() => useCase.ExecuteAsync(request));

        Assert.Empty(repository.Tasks);
    }
}
