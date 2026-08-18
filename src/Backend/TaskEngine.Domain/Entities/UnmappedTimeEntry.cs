namespace TaskEngine.Domain.Entities;

/// <summary>
/// A manually recorded block of time that was actually worked on a <see cref="TaskItem"/> away
/// from the computer (so it was never captured by a <see cref="WorkSession"/>'s activity
/// intervals), together with the user's justification. This entity only ever represents real
/// worked time - the "not invested in this task" option offered at closing time is not modeled
/// here at all: it simply discards the unmapped-time calculation without recording anything.
/// </summary>
public sealed class UnmappedTimeEntry
{
    public Guid Id { get; }
    public Guid TaskId { get; }
    public TimeSpan Duration { get; }
    public string Justification { get; }
    public DateTimeOffset RecordedAt { get; }

    private UnmappedTimeEntry(
        Guid id,
        Guid taskId,
        TimeSpan duration,
        string justification,
        DateTimeOffset recordedAt)
    {
        Id = id;
        TaskId = taskId;
        Duration = duration;
        Justification = justification;
        RecordedAt = recordedAt;
    }

    public static UnmappedTimeEntry Create(
        Guid taskId,
        TimeSpan duration,
        string justification,
        DateTimeOffset recordedAt)
    {
        if (duration <= TimeSpan.Zero)
        {
            throw new ArgumentException("Duration must be greater than zero.", nameof(duration));
        }

        if (string.IsNullOrWhiteSpace(justification))
        {
            throw new ArgumentException("Justification cannot be empty.", nameof(justification));
        }

        return new UnmappedTimeEntry(Guid.NewGuid(), taskId, duration, justification, recordedAt);
    }

    /// <summary>
    /// Rehydrates an <see cref="UnmappedTimeEntry"/> from persisted state, preserving its
    /// original <paramref name="id"/>. Unlike <see cref="Create"/>, this does not re-validate
    /// "new object" invariants - the data was already validated when the entry was first created.
    /// </summary>
    public static UnmappedTimeEntry Restore(
        Guid id,
        Guid taskId,
        TimeSpan duration,
        string justification,
        DateTimeOffset recordedAt)
    {
        return new UnmappedTimeEntry(id, taskId, duration, justification, recordedAt);
    }
}
