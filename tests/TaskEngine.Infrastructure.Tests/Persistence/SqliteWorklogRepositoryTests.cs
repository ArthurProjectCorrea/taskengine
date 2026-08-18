using TaskEngine.Domain.Entities;
using TaskEngine.Infrastructure.Persistence;

namespace TaskEngine.Infrastructure.Tests.Persistence;

public class SqliteWorklogRepositoryTests
{
    [Fact]
    public async Task AddAsync_ThenGetByIdAsync_RoundTripsUnapprovedWorklog()
    {
        using var db = new TempSqliteDatabase();
        await db.InitializeAsync();
        var repository = new SqliteWorklogRepository(db.PathProvider);

        var worklog = Worklog.Create(Guid.NewGuid(), Guid.NewGuid(), humanMinutes: 40, aiMinutes: 10);

        await repository.AddAsync(worklog, CancellationToken.None);
        var loaded = await repository.GetByIdAsync(worklog.Id, CancellationToken.None);

        Assert.NotNull(loaded);
        Assert.Equal(worklog.Id, loaded!.Id);
        Assert.Equal(worklog.WorkSessionId, loaded.WorkSessionId);
        Assert.Equal(worklog.TaskId, loaded.TaskId);
        Assert.Equal(40, loaded.HumanMinutes);
        Assert.Equal(10, loaded.AiMinutes);
        Assert.Null(loaded.ApprovedAt);
        Assert.Null(loaded.SyncedAt);
    }

    [Fact]
    public async Task UpdateAsync_PersistsApprovedAtAndSyncedAt()
    {
        using var db = new TempSqliteDatabase();
        await db.InitializeAsync();
        var repository = new SqliteWorklogRepository(db.PathProvider);

        var worklog = Worklog.Create(Guid.NewGuid(), Guid.NewGuid(), 40, 10);
        await repository.AddAsync(worklog, CancellationToken.None);

        var approvedAt = DateTimeOffset.UtcNow;
        worklog.Approve(approvedAt);
        await repository.UpdateAsync(worklog, CancellationToken.None);

        var afterApproval = await repository.GetByIdAsync(worklog.Id, CancellationToken.None);
        Assert.Equal(approvedAt, afterApproval!.ApprovedAt);
        Assert.Null(afterApproval.SyncedAt);

        var syncedAt = approvedAt.AddMinutes(1);
        worklog.MarkSynced(syncedAt);
        await repository.UpdateAsync(worklog, CancellationToken.None);

        var afterSync = await repository.GetByIdAsync(worklog.Id, CancellationToken.None);
        Assert.Equal(approvedAt, afterSync!.ApprovedAt);
        Assert.Equal(syncedAt, afterSync.SyncedAt);
    }

    [Fact]
    public async Task GetByIdAsync_WhenWorklogDoesNotExist_ReturnsNull()
    {
        using var db = new TempSqliteDatabase();
        await db.InitializeAsync();
        var repository = new SqliteWorklogRepository(db.PathProvider);

        var loaded = await repository.GetByIdAsync(Guid.NewGuid(), CancellationToken.None);

        Assert.Null(loaded);
    }

    [Fact]
    public async Task ListAsync_ReturnsAllPersistedWorklogs()
    {
        using var db = new TempSqliteDatabase();
        await db.InitializeAsync();
        var repository = new SqliteWorklogRepository(db.PathProvider);

        var first = Worklog.Create(Guid.NewGuid(), Guid.NewGuid(), 10, 0);
        var second = Worklog.Create(Guid.NewGuid(), Guid.NewGuid(), 20, 5);
        await repository.AddAsync(first, CancellationToken.None);
        await repository.AddAsync(second, CancellationToken.None);

        var worklogs = await repository.ListAsync(CancellationToken.None);

        Assert.Equal(2, worklogs.Count);
        Assert.Contains(worklogs, w => w.Id == first.Id);
        Assert.Contains(worklogs, w => w.Id == second.Id);
    }
}
