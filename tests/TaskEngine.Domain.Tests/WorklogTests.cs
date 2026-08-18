using TaskEngine.Domain.Entities;

namespace TaskEngine.Domain.Tests;

public class WorklogTests
{
    [Fact]
    public void Create_ComputesTotalMinutes()
    {
        var worklog = Worklog.Create(Guid.NewGuid(), Guid.NewGuid(), humanMinutes: 40, aiMinutes: 10);

        Assert.Equal(50, worklog.TotalMinutes);
    }

    [Fact]
    public void Approve_SetsApprovedAt()
    {
        var worklog = Worklog.Create(Guid.NewGuid(), Guid.NewGuid(), 40, 10);
        var approvedAt = DateTimeOffset.UtcNow;

        worklog.Approve(approvedAt);

        Assert.Equal(approvedAt, worklog.ApprovedAt);
    }

    [Fact]
    public void Approve_ThrowsWhenAlreadyApproved()
    {
        var worklog = Worklog.Create(Guid.NewGuid(), Guid.NewGuid(), 40, 10);
        worklog.Approve(DateTimeOffset.UtcNow);

        Assert.Throws<InvalidOperationException>(() => worklog.Approve(DateTimeOffset.UtcNow));
    }

    [Fact]
    public void MarkSynced_ThrowsWhenNotApproved()
    {
        var worklog = Worklog.Create(Guid.NewGuid(), Guid.NewGuid(), 40, 10);

        Assert.Throws<InvalidOperationException>(() => worklog.MarkSynced(DateTimeOffset.UtcNow));
    }

    [Fact]
    public void MarkSynced_SetsSyncedAtWhenApproved()
    {
        var worklog = Worklog.Create(Guid.NewGuid(), Guid.NewGuid(), 40, 10);
        worklog.Approve(DateTimeOffset.UtcNow);
        var syncedAt = DateTimeOffset.UtcNow;

        worklog.MarkSynced(syncedAt);

        Assert.Equal(syncedAt, worklog.SyncedAt);
    }

    [Fact]
    public void MarkSynced_ThrowsWhenAlreadySynced()
    {
        var worklog = Worklog.Create(Guid.NewGuid(), Guid.NewGuid(), 40, 10);
        worklog.Approve(DateTimeOffset.UtcNow);
        worklog.MarkSynced(DateTimeOffset.UtcNow);

        Assert.Throws<InvalidOperationException>(() => worklog.MarkSynced(DateTimeOffset.UtcNow));
    }
}
