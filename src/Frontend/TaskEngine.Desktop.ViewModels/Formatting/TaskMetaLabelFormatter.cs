using TaskEngine.Domain.Entities;

namespace TaskEngine.Desktop.ViewModels.Formatting;

/// <summary>
/// Formats a task's "{Provedor} · {IdDaTarefaNoProvedor}" meta label (or a "tarefa local" label
/// when it has no provider link) - was duplicated across <c>DashboardViewModel</c>,
/// <c>DetalhesTarefaViewModel</c> and <c>RelatorioViewModel</c>, each with its own private copy.
/// </summary>
public static class TaskMetaLabelFormatter
{
    public static string Build(TaskItem? task)
    {
        if (task is null)
        {
            return string.Empty;
        }

        return task.ProviderId is { } providerId
            ? task.ProviderTaskId is { } providerTaskId ? $"{providerId} · {providerTaskId}" : providerId
            : "Tarefa local (sem provedor vinculado)";
    }
}
