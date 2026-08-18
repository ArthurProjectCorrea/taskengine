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
}
