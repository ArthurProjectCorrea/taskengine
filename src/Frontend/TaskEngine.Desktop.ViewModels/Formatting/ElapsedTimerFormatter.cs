namespace TaskEngine.Desktop.ViewModels.Formatting;

/// <summary>
/// Formats a running/elapsed timer as total-hours:mm:ss (e.g. "27:41:08" past 24h) - distinct from
/// <see cref="DurationFormatter"/>'s "2h 15min" style, which is for a finished/summary duration,
/// not a live-updating clock. Was duplicated byte-for-byte in <c>DashboardViewModel</c> and
/// <c>DetalhesTarefaViewModel</c> (both show the same live timer for the selected/current task).
/// </summary>
public static class ElapsedTimerFormatter
{
    public static string Format(TimeSpan duration)
    {
        var totalHours = (int)duration.TotalHours;
        return $"{totalHours:D2}:{duration.Minutes:D2}:{duration.Seconds:D2}";
    }
}
