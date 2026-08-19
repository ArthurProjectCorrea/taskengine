using TaskEngine.Application.WorkSessions;
using TaskEngine.Domain.Entities;
using TaskStatus = TaskEngine.Domain.Entities.TaskStatus;

namespace TaskEngine.Application.Tests;

public class PauseWorkSessionUseCaseTests
{
    [Fact]
    public async Task ExecuteAsync_WithInProgressTask_PausesTaskAndClosesActiveSession()
    {
        var taskRepository = new FakeTaskRepository();
        var workSessionRepository = new FakeWorkSessionRepository();
        TaskItem task = TaskItem.Create("Write report");
        task.Start();
        await taskRepository.AddAsync(task, CancellationToken.None);
        WorkSession activeSession = WorkSession.Start(task.Id, DateTimeOffset.UtcNow.AddMinutes(-10));
        await workSessionRepository.AddAsync(activeSession, CancellationToken.None);
        var useCase = new PauseWorkSessionUseCase(taskRepository, workSessionRepository);

        WorkSessionDto result = await useCase.ExecuteAsync(task.Id);

        Assert.Equal(TaskStatus.Paused, task.Status);
        Assert.Equal(2, workSessionRepository.Sessions.Count);

        WorkSession closedActive = workSessionRepository.Sessions.Single(s => s.Id == activeSession.Id);
        Assert.False(closedActive.IsOpen);

        WorkSession pauseSession = workSessionRepository.Sessions.Single(s => s.Id == result.Id);
        Assert.Equal(WorkSessionType.Pause, pauseSession.Type);
        Assert.Equal(WorkSessionOrigin.System, pauseSession.Origin);
        Assert.True(pauseSession.IsOpen);
    }

    [Fact]
    public async Task ExecuteAsync_WithProviderOrigin_RecordsProviderOriginOnThePauseSession()
    {
        var taskRepository = new FakeTaskRepository();
        var workSessionRepository = new FakeWorkSessionRepository();
        TaskItem task = TaskItem.Create("Write report");
        task.Start();
        await taskRepository.AddAsync(task, CancellationToken.None);
        var useCase = new PauseWorkSessionUseCase(taskRepository, workSessionRepository);

        WorkSessionDto result = await useCase.ExecuteAsync(task.Id, WorkSessionOrigin.Provider);

        Assert.Equal(nameof(WorkSessionOrigin.Provider), result.Origin);
    }

    [Fact]
    public async Task ExecuteAsync_WithTaskNotInProgress_ThrowsAndDoesNotCreatePauseSession()
    {
        var taskRepository = new FakeTaskRepository();
        var workSessionRepository = new FakeWorkSessionRepository();
        TaskItem task = TaskItem.Create("Write report");
        await taskRepository.AddAsync(task, CancellationToken.None);
        var useCase = new PauseWorkSessionUseCase(taskRepository, workSessionRepository);

        await Assert.ThrowsAsync<InvalidOperationException>(() => useCase.ExecuteAsync(task.Id));

        Assert.Empty(workSessionRepository.Sessions);
    }

    [Fact]
    public async Task ExecuteAsync_WithUnknownTask_Throws()
    {
        var taskRepository = new FakeTaskRepository();
        var workSessionRepository = new FakeWorkSessionRepository();
        var useCase = new PauseWorkSessionUseCase(taskRepository, workSessionRepository);

        await Assert.ThrowsAsync<InvalidOperationException>(() => useCase.ExecuteAsync(Guid.NewGuid()));
    }
}
