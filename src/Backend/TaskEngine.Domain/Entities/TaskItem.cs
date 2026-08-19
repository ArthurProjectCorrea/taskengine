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

    /// <summary>
    /// Raw status label as reported by the provider (e.g. "Blocked", "In Review"), kept
    /// alongside the derived <see cref="Status"/> classification — see RN-010 (ERS-Tarefas.md).
    /// Null for tasks with no provider-reported status yet (e.g. created locally without a
    /// provider link).
    /// </summary>
    public string? ProviderStatusName { get; private set; }

    /// <summary>
    /// Priority label as reported by the provider's dynamic schema (Schema-001 "Prioridade",
    /// ERS-Tarefas.md), e.g. "High"/"P1". Optional - not every provider/project configures a
    /// priority field. Kept as free text rather than a fixed enum since priority *options* are
    /// provider-defined, same rationale as <see cref="ProviderStatusName"/>/RN-010; per the ERS
    /// assumptions, no cross-provider normalization is needed yet (only one provider is connected
    /// at a time in this version).
    /// </summary>
    public string? Priority { get; private set; }

    public string? ProviderId { get; private set; }
    public string? ProviderTaskId { get; private set; }
    public DateTimeOffset CreatedAt { get; }

    private TaskItem(
        Guid id,
        string title,
        string? description,
        string? providerId,
        string? providerTaskId,
        DateTimeOffset createdAt)
    {
        Id = id;
        Title = title;
        Description = description;
        ProviderId = providerId;
        ProviderTaskId = providerTaskId;
        CreatedAt = createdAt;
        Status = TaskStatus.ToDo;
    }

    /// <summary>
    /// <paramref name="providerId"/>/<paramref name="providerTaskId"/> are accepted here (instead
    /// of only via <see cref="AttachProviderReference"/>) for symmetry with <see cref="Restore"/>
    /// and to allow seeding an already-linked task in one call (tests, fixtures). In practice the
    /// Application layer's task creation flow still creates the task first and calls
    /// <see cref="AttachProviderReference"/> afterwards, since the provider task id is only known
    /// once the remote provider call returns.
    /// </summary>
    public static TaskItem Create(
        string title,
        string? description = null,
        string? providerTaskId = null,
        string? providerId = null,
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
            providerId,
            providerTaskId,
            createdAt ?? DateTimeOffset.UtcNow);
    }

    /// <summary>
    /// Rehydrates a <see cref="TaskItem"/> from persisted state. Unlike <see cref="Create"/>,
    /// this does not re-validate "new object" invariants (e.g. non-empty title) - the data was
    /// already validated when the entity was first created.
    /// </summary>
    public static TaskItem Restore(
        Guid id,
        string title,
        string? description,
        TaskStatus status,
        string? providerId,
        string? providerTaskId,
        DateTimeOffset createdAt,
        string? providerStatusName = null,
        string? priority = null)
    {
        return new TaskItem(id, title, description, providerId, providerTaskId, createdAt)
        {
            Status = status,
            ProviderStatusName = providerStatusName,
            Priority = priority,
        };
    }

    public void Start()
    {
        if (Status != TaskStatus.ToDo)
        {
            throw new InvalidOperationException($"Cannot start a task in status '{Status}'.");
        }

        Status = TaskStatus.InProgress;
    }

    /// <summary>
    /// Pauses time tracking for a task that is currently in progress — see RF-015/RN-011
    /// (ERS-Tarefas.md). Any provider status other than "in progress"/"done" triggers this, with
    /// no special-casing by status name.
    /// </summary>
    public void Pause()
    {
        if (Status != TaskStatus.InProgress)
        {
            throw new InvalidOperationException($"Cannot pause a task in status '{Status}'.");
        }

        Status = TaskStatus.Paused;
    }

    /// <summary>
    /// Resumes a paused task, reopening time tracking — see RF-015/CA-015.3 (ERS-Tarefas.md).
    /// </summary>
    public void Resume()
    {
        if (Status != TaskStatus.Paused)
        {
            throw new InvalidOperationException($"Cannot resume a task in status '{Status}'.");
        }

        Status = TaskStatus.InProgress;
    }

    /// <summary>
    /// Completes a task that has been started — either actively in progress or currently paused
    /// (e.g. the user pauses, then concludes without resuming first).
    /// </summary>
    public void Complete()
    {
        if (Status != TaskStatus.InProgress && Status != TaskStatus.Paused)
        {
            throw new InvalidOperationException($"Cannot complete a task in status '{Status}'.");
        }

        Status = TaskStatus.Done;
    }

    /// <summary>
    /// Records the raw status label last reported by the provider, independent of the derived
    /// <see cref="Status"/> classification. Callers (e.g. the sync use case) are responsible for
    /// separately calling <see cref="Pause"/>/<see cref="Resume"/>/<see cref="Complete"/> based on
    /// how that raw label classifies (RN-010/RN-011).
    /// </summary>
    public void SetProviderStatusName(string? providerStatusName)
    {
        ProviderStatusName = providerStatusName;
    }

    /// <summary>
    /// Records the priority label reported by the provider's dynamic schema (Schema-001).
    /// </summary>
    public void SetPriority(string? priority)
    {
        Priority = priority;
    }

    /// <summary>
    /// Links this task to the task/item created for it on an external provider. No guard against
    /// reattaching to a different provider/provider task id - overwriting is an accepted, simpler
    /// behavior.
    /// </summary>
    public void AttachProviderReference(string providerId, string providerTaskId)
    {
        if (string.IsNullOrWhiteSpace(providerId))
        {
            throw new ArgumentException("Provider id is required.", nameof(providerId));
        }

        if (string.IsNullOrWhiteSpace(providerTaskId))
        {
            throw new ArgumentException("Provider task id is required.", nameof(providerTaskId));
        }

        ProviderId = providerId;
        ProviderTaskId = providerTaskId;
    }
}
