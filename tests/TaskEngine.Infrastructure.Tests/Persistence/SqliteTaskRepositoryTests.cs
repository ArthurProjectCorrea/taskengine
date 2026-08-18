using TaskEngine.Domain.Entities;
using TaskEngine.Infrastructure.Persistence;

using TaskStatus = TaskEngine.Domain.Entities.TaskStatus;

namespace TaskEngine.Infrastructure.Tests.Persistence;

public class SqliteTaskRepositoryTests
{
    [Fact]
    public async Task AddAsync_ThenGetByIdAsync_RoundTripsAllFields()
    {
        using var db = new TempSqliteDatabase();
        await db.InitializeAsync();
        var repository = new SqliteTaskRepository(db.PathProvider);

        var task = TaskItem.Create("Write report", "Quarterly summary", "gh-123");
        task.Start();

        await repository.AddAsync(task, CancellationToken.None);
        var loaded = await repository.GetByIdAsync(task.Id, CancellationToken.None);

        Assert.NotNull(loaded);
        Assert.Equal(task.Id, loaded!.Id);
        Assert.Equal(task.Title, loaded.Title);
        Assert.Equal(task.Description, loaded.Description);
        Assert.Equal(task.Status, loaded.Status);
        Assert.Equal(task.ProviderTaskId, loaded.ProviderTaskId);
        Assert.Equal(task.CreatedAt, loaded.CreatedAt);
    }

    [Fact]
    public async Task AddAsync_WithNullDescriptionAndProviderTaskId_RoundTripsAsNull()
    {
        using var db = new TempSqliteDatabase();
        await db.InitializeAsync();
        var repository = new SqliteTaskRepository(db.PathProvider);

        var task = TaskItem.Create("Minimal task");

        await repository.AddAsync(task, CancellationToken.None);
        var loaded = await repository.GetByIdAsync(task.Id, CancellationToken.None);

        Assert.NotNull(loaded);
        Assert.Null(loaded!.Description);
        Assert.Null(loaded.ProviderTaskId);
        Assert.Equal(TaskStatus.ToDo, loaded.Status);
    }

    [Fact]
    public async Task GetByIdAsync_WhenTaskDoesNotExist_ReturnsNull()
    {
        using var db = new TempSqliteDatabase();
        await db.InitializeAsync();
        var repository = new SqliteTaskRepository(db.PathProvider);

        var loaded = await repository.GetByIdAsync(Guid.NewGuid(), CancellationToken.None);

        Assert.Null(loaded);
    }

    [Fact]
    public async Task ListAsync_ReturnsAllPersistedTasks()
    {
        using var db = new TempSqliteDatabase();
        await db.InitializeAsync();
        var repository = new SqliteTaskRepository(db.PathProvider);

        var first = TaskItem.Create("First task");
        var second = TaskItem.Create("Second task", createdAt: first.CreatedAt.AddMinutes(1));
        await repository.AddAsync(first, CancellationToken.None);
        await repository.AddAsync(second, CancellationToken.None);

        var tasks = await repository.ListAsync(CancellationToken.None);

        Assert.Equal(2, tasks.Count);
        Assert.Contains(tasks, t => t.Id == first.Id);
        Assert.Contains(tasks, t => t.Id == second.Id);
    }
}
