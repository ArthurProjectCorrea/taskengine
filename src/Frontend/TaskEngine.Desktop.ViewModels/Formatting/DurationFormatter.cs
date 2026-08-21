namespace TaskEngine.Desktop.ViewModels.Formatting;

/// <summary>
/// Single shared duration-to-label formatter, replacing the near-identical private
/// <c>FormatDuration</c> copies previously duplicated across <c>ConcludeTaskModalViewModel</c>,
/// <c>DashboardViewModel</c>, <c>DetalhesTarefaViewModel</c>, <c>RelatorioViewModel</c> and
/// <c>TarefasViewModel</c>. Those older versions always rounded to whole minutes, so anything
/// under 30s displayed as "0min" - indistinguishable from genuinely zero activity. This version
/// picks the coarsest unit that still says something real: hours+minutes once there's at least an
/// hour, minutes once there's at least a minute, otherwise seconds.
///
/// A near-zero duration (under 1 second) still formats as "0min", not an empty string - this is a
/// persistent label used by always-visible summary fields (KPI cards, detail-panel bars) that need
/// *something* to show for "no activity yet". Hiding a genuinely negligible activity item entirely
/// is a *list-membership* decision for whoever is building that list (see
/// <c>ConcludeTaskModalViewModel</c>'s own zero-duration filter), not something this formatter
/// should do by returning a blank label out from under an always-shown field.
/// </summary>
public static class DurationFormatter
{
    public static string Format(TimeSpan duration)
    {
        if (duration < TimeSpan.Zero)
        {
            duration = TimeSpan.Zero;
        }

        if (duration < TimeSpan.FromSeconds(1))
        {
            return "0min";
        }

        if (duration < TimeSpan.FromMinutes(1))
        {
            return $"{(int)Math.Round(duration.TotalSeconds)}s";
        }

        if (duration < TimeSpan.FromHours(1))
        {
            return $"{(int)Math.Round(duration.TotalMinutes)}min";
        }

        var totalMinutes = (int)Math.Round(duration.TotalMinutes);
        var hours = totalMinutes / 60;
        var minutes = totalMinutes % 60;
        return minutes > 0 ? $"{hours}h {minutes}min" : $"{hours}h";
    }
}
