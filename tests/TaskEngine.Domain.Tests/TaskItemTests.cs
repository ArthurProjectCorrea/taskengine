using TaskEngine.Domain.Entities;
using TaskStatus = TaskEngine.Domain.Entities.TaskStatus;

namespace TaskEngine.Domain.Tests;

public class TaskItemTests
{
    [Fact]
    public void Create_StartsInToDoStatus()
    {
        var task = TaskItem.Create("Write tests");

        Assert.Equal(TaskStatus.ToDo, task.Status);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Create_ThrowsWhenTitleIsEmpty(string? title)
    {
        Assert.Throws<ArgumentException>(() => TaskItem.Create(title!));
    }

    [Fact]
    public void Start_TransitionsFromToDoToInProgress()
    {
        var task = TaskItem.Create("Write tests");

        task.Start();

        Assert.Equal(TaskStatus.InProgress, task.Status);
    }

    [Fact]
    public void Start_ThrowsWhenNotToDo()
    {
        var task = TaskItem.Create("Write tests");
        task.Start();

        Assert.Throws<InvalidOperationException>(() => task.Start());
    }

    [Fact]
    public void Complete_TransitionsFromInProgressToDone()
    {
        var task = TaskItem.Create("Write tests");
        task.Start();

        task.Complete();

        Assert.Equal(TaskStatus.Done, task.Status);
    }

    [Fact]
    public void Complete_ThrowsWhenNotInProgress()
    {
        var task = TaskItem.Create("Write tests");

        Assert.Throws<InvalidOperationException>(() => task.Complete());
    }

    [Fact]
    public void Complete_TransitionsFromPausedToDone()
    {
        var task = TaskItem.Create("Write tests");
        task.Start();
        task.Pause();

        task.Complete();

        Assert.Equal(TaskStatus.Done, task.Status);
    }

    [Fact]
    public void Pause_TransitionsFromInProgressToPaused()
    {
        var task = TaskItem.Create("Write tests");
        task.Start();

        task.Pause();

        Assert.Equal(TaskStatus.Paused, task.Status);
    }

    [Fact]
    public void Pause_ThrowsWhenNotInProgress()
    {
        var task = TaskItem.Create("Write tests");

        Assert.Throws<InvalidOperationException>(() => task.Pause());
    }

    [Fact]
    public void Resume_TransitionsFromPausedToInProgress()
    {
        var task = TaskItem.Create("Write tests");
        task.Start();
        task.Pause();

        task.Resume();

        Assert.Equal(TaskStatus.InProgress, task.Status);
    }

    [Fact]
    public void Resume_ThrowsWhenNotPaused()
    {
        var task = TaskItem.Create("Write tests");
        task.Start();

        Assert.Throws<InvalidOperationException>(() => task.Resume());
    }

    [Fact]
    public void SetProviderStatusName_UpdatesRawProviderStatus()
    {
        var task = TaskItem.Create("Write tests");

        task.SetProviderStatusName("Blocked");

        Assert.Equal("Blocked", task.ProviderStatusName);
    }

    [Fact]
    public void Create_StartsWithNullPriority()
    {
        var task = TaskItem.Create("Write tests");

        Assert.Null(task.Priority);
    }

    [Fact]
    public void SetPriority_UpdatesPriority()
    {
        var task = TaskItem.Create("Write tests");

        task.SetPriority("High");

        Assert.Equal("High", task.Priority);
    }

    [Fact]
    public void CompleteOffline_TransitionsFromInProgressToDonePendingSync()
    {
        var task = TaskItem.Create("Write tests");
        task.Start();

        task.CompleteOffline();

        Assert.Equal(TaskStatus.DonePendingSync, task.Status);
    }

    [Fact]
    public void CompleteOffline_TransitionsFromPausedToDonePendingSync()
    {
        var task = TaskItem.Create("Write tests");
        task.Start();
        task.Pause();

        task.CompleteOffline();

        Assert.Equal(TaskStatus.DonePendingSync, task.Status);
    }

    [Fact]
    public void CompleteOffline_ThrowsWhenNotInProgressOrPaused()
    {
        var task = TaskItem.Create("Write tests");

        Assert.Throws<InvalidOperationException>(() => task.CompleteOffline());
    }

    [Fact]
    public void MarkSynced_TransitionsFromDonePendingSyncToDone()
    {
        var task = TaskItem.Create("Write tests");
        task.Start();
        task.CompleteOffline();

        task.MarkSynced();

        Assert.Equal(TaskStatus.Done, task.Status);
    }

    [Fact]
    public void MarkSynced_ThrowsWhenNotDonePendingSync()
    {
        var task = TaskItem.Create("Write tests");

        Assert.Throws<InvalidOperationException>(() => task.MarkSynced());
    }

    [Fact]
    public void AttachProviderReference_SetsProviderIdAndProviderTaskId()
    {
        var task = TaskItem.Create("Write tests");

        task.AttachProviderReference("github", "gh-42");

        Assert.Equal("github", task.ProviderId);
        Assert.Equal("gh-42", task.ProviderTaskId);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void AttachProviderReference_ThrowsWhenProviderIdIsEmpty(string? providerId)
    {
        var task = TaskItem.Create("Write tests");

        Assert.Throws<ArgumentException>(() => task.AttachProviderReference(providerId!, "gh-42"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void AttachProviderReference_ThrowsWhenProviderTaskIdIsEmpty(string? providerTaskId)
    {
        var task = TaskItem.Create("Write tests");

        Assert.Throws<ArgumentException>(() => task.AttachProviderReference("github", providerTaskId!));
    }
}
