using TaskEngine.Domain.Entities;

namespace TaskEngine.Domain.Monitoring;

/// <summary>
/// Formalizes the origin-classification heuristic behind RF-003 (ERS-Monitoramento.md): whether an
/// <see cref="ActivityInterval"/> was produced by a human or by an AI agent. A process name is
/// classified as <see cref="ActivitySource.Ai"/> when it matches a known AI coding
/// agent/CLI/assistant, and <see cref="ActivitySource.Human"/> otherwise - this is a pragmatic
/// approximation (the monitoring module has no direct signal of "who" touched a file or focused a
/// window, only which processes are involved), consistent with issue #12's premise that this
/// module exists specifically to catch background edits made by tools like Claude Code or GitHub
/// Copilot while the user isn't at the keyboard.
/// <see cref="ClassifyFromRunningProcesses"/> is what <c>FileSystemActivityWatcher</c> (RF-001)
/// uses: a file change is attributed to AI when any known AI process happens to be running
/// concurrently, since <see cref="System.IO.FileSystemWatcher"/> itself carries no information
/// about which process made the change. RF-002 (window/browser focus) does not use this
/// classifier - see <c>WindowFocusActivityWatcher</c>'s remarks for why that activity is always
/// human.
/// </summary>
public static class ActivitySourceClassifier
{
    /// <summary>
    /// Process names (case-insensitive substring match) recognized as AI coding agents/CLIs/
    /// assistants. Deliberately a flat list rather than exact matches, since these tools are often
    /// invoked through a wrapping shell/host process whose name embeds the tool's own name (e.g.
    /// <c>claude.exe</c>, <c>node</c> running a <c>claude-code</c> script, etc.) - a substring
    /// match is more robust to that than an exact one, at the cost of being an approximation.
    /// </summary>
    private static readonly string[] KnownAiProcessNames =
    [
        "claude", "copilot", "cursor", "aider", "codex", "windsurf", "amazonq", "continue", "cody",
    ];

    /// <summary>
    /// Classifies a single process name as the origin of an activity.
    /// </summary>
    public static ActivitySource Classify(string? processName)
    {
        if (string.IsNullOrWhiteSpace(processName))
        {
            return ActivitySource.Human;
        }

        foreach (var known in KnownAiProcessNames)
        {
            if (processName.Contains(known, StringComparison.OrdinalIgnoreCase))
            {
                return ActivitySource.Ai;
            }
        }

        return ActivitySource.Human;
    }

    /// <summary>
    /// Classifies an activity given every process currently running: AI if any one of them is a
    /// known AI agent, human otherwise. Used where the activity itself (e.g. a file-system change)
    /// carries no direct process attribution, only the set of processes running around the time it
    /// happened.
    /// </summary>
    public static ActivitySource ClassifyFromRunningProcesses(IEnumerable<string> runningProcessNames)
    {
        foreach (var processName in runningProcessNames)
        {
            if (Classify(processName) == ActivitySource.Ai)
            {
                return ActivitySource.Ai;
            }
        }

        return ActivitySource.Human;
    }
}
