using TaskEngine.Application.WorkSessions;
using TaskEngine.Domain.Entities;
using TaskStatus = TaskEngine.Domain.Entities.TaskStatus;

namespace TaskEngine.Application.Tests;

public class StartWorkSessionUseCaseTests
{
    [Fact]
    public async Task ExecuteAsync_WithToDoTask_StartsTaskAndOpensSession()
    {
        var taskRepository = new FakeTaskRepository();
        var workSessionRepository = new FakeWorkSessionRepository();
        TaskItem task = TaskItem.Create("Write report");
        await taskRepository.AddAsync(task, CancellationToken.None);
        var useCase = new StartWorkSessionUseCase(taskRepository, workSessionRepository);

        WorkSessionDto result = await useCase.ExecuteAsync(task.Id);

        Assert.Equal(TaskStatus.InProgress, task.Status);
        WorkSession persisted = Assert.Single(workSessionRepository.Sessions);
        Assert.Equal(task.Id, persisted.TaskId);
        Assert.True(persisted.IsOpen);

        Assert.Equal(persisted.Id, result.Id);
        Assert.Equal(task.Id, result.TaskId);
        Assert.Equal(persisted.StartedAt, result.StartedAt);
        Assert.Null(result.EndedAt);
    }

    [Fact]
    public async Task ExecuteAsync_WithTaskNotToDo_ThrowsAndDoesNotCreateSession()
    {
        var taskRepository = new FakeTaskRepository();
        var workSessionRepository = new FakeWorkSessionRepository();
        TaskItem task = TaskItem.Create("Write report");
        task.Start();
        await taskRepository.AddAsync(task, CancellationToken.None);
        var useCase = new StartWorkSessionUseCase(taskRepository, workSessionRepository);

        await Assert.ThrowsAsync<InvalidOperationException>(() => useCase.ExecuteAsync(task.Id));

        Assert.Empty(workSessionRepository.Sessions);
    }

    [Fact]
    public async Task ExecuteAsync_WithUnknownTask_ThrowsAndDoesNotCreateSession()
    {
        var taskRepository = new FakeTaskRepository();
        var workSessionRepository = new FakeWorkSessionRepository();
        var useCase = new StartWorkSessionUseCase(taskRepository, workSessionRepository);

        await Assert.ThrowsAsync<InvalidOperationException>(() => useCase.ExecuteAsync(Guid.NewGuid()));

        Assert.Empty(workSessionRepository.Sessions);
    }
}
