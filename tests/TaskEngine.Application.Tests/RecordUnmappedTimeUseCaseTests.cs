using TaskEngine.Application.UnmappedTime;

namespace TaskEngine.Application.Tests;

public class RecordUnmappedTimeUseCaseTests
{
    [Fact]
    public async Task ExecuteAsync_WithValidData_PersistsAndReturnsDto()
    {
        var repository = new FakeUnmappedTimeEntryRepository();
        var useCase = new RecordUnmappedTimeUseCase(repository);
        var taskId = Guid.NewGuid();
        var duration = TimeSpan.FromMinutes(45);

        UnmappedTimeEntryDto dto = await useCase.ExecuteAsync(taskId, duration, "Whiteboard session with the team.");

        Assert.Single(repository.Entries);
        Assert.Equal(taskId, dto.TaskId);
        Assert.Equal(duration, dto.Duration);
        Assert.Equal("Whiteboard session with the team.", dto.Justification);
        Assert.NotEqual(Guid.Empty, dto.Id);
        Assert.Equal(repository.Entries[0].Id, dto.Id);
    }

    [Fact]
    public async Task ExecuteAsync_WithZeroDuration_ThrowsAndDoesNotPersist()
    {
        var repository = new FakeUnmappedTimeEntryRepository();
        var useCase = new RecordUnmappedTimeUseCase(repository);

        await Assert.ThrowsAsync<ArgumentException>(
            () => useCase.ExecuteAsync(Guid.NewGuid(), TimeSpan.Zero, "Justification"));

        Assert.Empty(repository.Entries);
    }

    [Fact]
    public async Task ExecuteAsync_WithEmptyJustification_ThrowsAndDoesNotPersist()
    {
        var repository = new FakeUnmappedTimeEntryRepository();
        var useCase = new RecordUnmappedTimeUseCase(repository);

        await Assert.ThrowsAsync<ArgumentException>(
            () => useCase.ExecuteAsync(Guid.NewGuid(), TimeSpan.FromMinutes(10), " "));

        Assert.Empty(repository.Entries);
    }
}
