namespace TaskEngine.Domain.Entities;

/// <summary>
/// A unit of work tracked by TaskEngine. Named <c>TaskItem</c> instead of <c>Task</c> to avoid
/// colliding with <see cref="System.Threading.Tasks.Task"/>.
/// </summary>
public sealed class TaskItem
{
    public Guid Id { get; }
    public string Title { get; private set; }
    public string? Description { get; private set; }
    public TaskStatus Status { get; private set; }
    public string? ProviderTaskId { get; private set; }
    public DateTimeOffset CreatedAt { get; }

    private TaskItem(
        Guid id,
        string title,
        string? description,
        string? providerTaskId,
        DateTimeOffset createdAt)
    {
        Id = id;
        Title = title;
        Description = description;
        ProviderTaskId = providerTaskId;
        CreatedAt = createdAt;
        Status = TaskStatus.ToDo;
    }

    public static TaskItem Create(
        string title,
        string? description = null,
        string? providerTaskId = null,
        DateTimeOffset? createdAt = null)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            throw new ArgumentException("Title is required.", nameof(title));
        }

        return new TaskItem(
            Guid.NewGuid(),
            title,
            description,
            providerTaskId,
            createdAt ?? DateTimeOffset.UtcNow);
    }

    public void Start()
    {
        if (Status != TaskStatus.ToDo)
        {
            throw new InvalidOperationException($"Cannot start a task in status '{Status}'.");
        }

        Status = TaskStatus.InProgress;
    }

    public void Complete()
    {
        if (Status != TaskStatus.InProgress)
        {
            throw new InvalidOperationException($"Cannot complete a task in status '{Status}'.");
        }

        Status = TaskStatus.Done;
    }
}
