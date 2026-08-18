namespace TaskEngine.Domain.Entities;

/// <summary>
/// Consolidated time summary of a <see cref="WorkSession"/>, ready for human approval and
/// sync with the external provider.
/// </summary>
public sealed class Worklog
{
    public Guid Id { get; }
    public Guid WorkSessionId { get; }
    public Guid TaskId { get; }
    public double HumanMinutes { get; }
    public double AiMinutes { get; }
    public DateTimeOffset? ApprovedAt { get; private set; }
    public DateTimeOffset? SyncedAt { get; private set; }

    public double TotalMinutes => HumanMinutes + AiMinutes;

    private Worklog(Guid id, Guid workSessionId, Guid taskId, double humanMinutes, double aiMinutes)
    {
        Id = id;
        WorkSessionId = workSessionId;
        TaskId = taskId;
        HumanMinutes = humanMinutes;
        AiMinutes = aiMinutes;
    }

    public static Worklog Create(Guid workSessionId, Guid taskId, double humanMinutes, double aiMinutes)
    {
        return new Worklog(Guid.NewGuid(), workSessionId, taskId, humanMinutes, aiMinutes);
    }

    public void Approve(DateTimeOffset approvedAt)
    {
        if (ApprovedAt is not null)
        {
            throw new InvalidOperationException("Worklog is already approved.");
        }

        ApprovedAt = approvedAt;
    }

    public void MarkSynced(DateTimeOffset syncedAt)
    {
        if (ApprovedAt is null)
        {
            throw new InvalidOperationException("Cannot sync a worklog that has not been approved.");
        }

        if (SyncedAt is not null)
        {
            throw new InvalidOperationException("Worklog is already synced.");
        }

        SyncedAt = syncedAt;
    }
}
