using TaskEngine.Application.WorkSessions;
using TaskEngine.Domain.Entities;
using TaskStatus = TaskEngine.Domain.Entities.TaskStatus;

namespace TaskEngine.Application.Tests;

public class EndWorkSessionUseCaseTests
{
    [Fact]
    public async Task ExecuteAsync_WithOpenSession_CompletesTaskAndClosesSession()
    {
        var taskRepository = new FakeTaskRepository();
        var workSessionRepository = new FakeWorkSessionRepository();
        TaskItem task = TaskItem.Create("Write report");
        task.Start();
        await taskRepository.AddAsync(task, CancellationToken.None);
        WorkSession session = WorkSession.Start(task.Id, DateTimeOffset.UtcNow.AddMinutes(-30));
        await workSessionRepository.AddAsync(session, CancellationToken.None);
        var useCase = new EndWorkSessionUseCase(taskRepository, workSessionRepository);

        WorkSessionDto result = await useCase.ExecuteAsync(task.Id);

        Assert.Equal(TaskStatus.Done, task.Status);
        WorkSession persisted = Assert.Single(workSessionRepository.Sessions);
        Assert.False(persisted.IsOpen);
        Assert.NotNull(persisted.EndedAt);

        Assert.Equal(persisted.Id, result.Id);
        Assert.Equal(task.Id, result.TaskId);
        Assert.Equal(persisted.EndedAt, result.EndedAt);
    }

    [Fact]
    public async Task ExecuteAsync_WithoutOpenSession_ThrowsAndDoesNotCompleteTask()
    {
        var taskRepository = new FakeTaskRepository();
        var workSessionRepository = new FakeWorkSessionRepository();
        TaskItem task = TaskItem.Create("Write report");
        task.Start();
        await taskRepository.AddAsync(task, CancellationToken.None);
        var useCase = new EndWorkSessionUseCase(taskRepository, workSessionRepository);

        await Assert.ThrowsAsync<InvalidOperationException>(() => useCase.ExecuteAsync(task.Id));

        Assert.Equal(TaskStatus.InProgress, task.Status);
    }

    [Fact]
    public async Task ExecuteAsync_WithUnknownTask_Throws()
    {
        var taskRepository = new FakeTaskRepository();
        var workSessionRepository = new FakeWorkSessionRepository();
        var useCase = new EndWorkSessionUseCase(taskRepository, workSessionRepository);

        await Assert.ThrowsAsync<InvalidOperationException>(() => useCase.ExecuteAsync(Guid.NewGuid()));
    }
}
