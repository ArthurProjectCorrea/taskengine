using TaskEngine.Application.Reports;
using TaskEngine.Desktop.ViewModels;
using TaskEngine.Desktop.ViewModels.Navigation;
using TaskEngine.Domain.Entities;

namespace TaskEngine.Desktop.Tests;

public class RelatorioViewModelTests
{
    private static RelatorioViewModel CreateViewModel(
        FakeTaskRepository taskRepository,
        FakeWorkSessionRepository workSessionRepository,
        FakeReportFileSaveDialog? reportFileSaveDialog = null,
        FakeNavigationService? navigationService = null)
    {
        var generateTaskActivityTimelineUseCase = new GenerateTaskActivityTimelineUseCase(taskRepository, workSessionRepository);

        return new RelatorioViewModel(
            taskRepository,
            generateTaskActivityTimelineUseCase,
            reportFileSaveDialog ?? new FakeReportFileSaveDialog(),
            navigationService ?? new FakeNavigationService());
    }

    private static async Task<TaskItem> CreateDoneTaskWithActivitiesAsync(
        FakeTaskRepository taskRepository, FakeWorkSessionRepository workSessionRepository)
    {
        TaskItem task = TaskItem.Create("Corrigir bug de sincronização");
        task.Start();
        await taskRepository.AddAsync(task, CancellationToken.None);

        var startedAt = new DateTimeOffset(2026, 1, 10, 9, 0, 0, TimeSpan.Zero);
        WorkSession session = WorkSession.Start(task.Id, startedAt);
        session.RecordActivity(ActivitySource.Human, startedAt, startedAt.AddMinutes(10), ActivityItemType.File, "src/a.cs");
        session.RecordActivity(ActivitySource.Ai, startedAt.AddMinutes(10), startedAt.AddMinutes(20), ActivityItemType.File, "src/b.cs");
        session.ApplyActivitySelection(new HashSet<Guid> { session.Activities[0].Id, session.Activities[1].Id });
        session.End(startedAt.AddMinutes(20));
        await workSessionRepository.AddAsync(session, CancellationToken.None);

        task.Complete();
        await taskRepository.UpdateAsync(task, CancellationToken.None);

        return task;
    }

    [Fact]
    public async Task ApplyParameter_WithADoneTask_LoadsTitleAndBuildsOneLanePerNonOverlappingItem()
    {
        var taskRepository = new FakeTaskRepository();
        var workSessionRepository = new FakeWorkSessionRepository();
        TaskItem task = await CreateDoneTaskWithActivitiesAsync(taskRepository, workSessionRepository);

        var viewModel = CreateViewModel(taskRepository, workSessionRepository);
        viewModel.ApplyParameter(task.Id);
        await viewModel.LoadAsync();

        Assert.True(viewModel.HasData);
        Assert.False(viewModel.IsUnavailable);
        Assert.Equal("Corrigir bug de sincronização", viewModel.Title);
        Assert.True(viewModel.HasActivities);
        Assert.False(viewModel.NoActivities);

        // Both recorded activities are sequential (0-10min, 10-20min) - CA-010.1 does not require
        // separate lanes when items don't actually overlap.
        var lane = Assert.Single(viewModel.Lanes);
        Assert.Equal(2, lane.Bars.Count);
        Assert.Equal("src/a.cs", lane.Bars[0].Label);
        Assert.Equal("src/b.cs", lane.Bars[1].Label);
    }

    [Fact]
    public async Task ApplyParameter_WithOverlappingSelectedActivities_UsesSeparateLanes()
    {
        var taskRepository = new FakeTaskRepository();
        var workSessionRepository = new FakeWorkSessionRepository();
        TaskItem task = TaskItem.Create("Tarefa com sobreposição");
        task.Start();
        await taskRepository.AddAsync(task, CancellationToken.None);

        var startedAt = new DateTimeOffset(2026, 1, 10, 9, 0, 0, TimeSpan.Zero);
        WorkSession session = WorkSession.Start(task.Id, startedAt);
        session.RecordActivity(ActivitySource.Human, startedAt, startedAt.AddMinutes(30), ActivityItemType.File, "src/a.cs");
        session.RecordActivity(ActivitySource.Human, startedAt.AddMinutes(15), startedAt.AddMinutes(45), ActivityItemType.File, "src/b.cs");
        session.ApplyActivitySelection(new HashSet<Guid> { session.Activities[0].Id, session.Activities[1].Id });
        session.End(startedAt.AddMinutes(45));
        await workSessionRepository.AddAsync(session, CancellationToken.None);

        task.Complete();
        await taskRepository.UpdateAsync(task, CancellationToken.None);

        var viewModel = CreateViewModel(taskRepository, workSessionRepository);
        viewModel.ApplyParameter(task.Id);
        await viewModel.LoadAsync();

        Assert.Equal(2, viewModel.Lanes.Count);
        Assert.Equal("src/a.cs", Assert.Single(viewModel.Lanes[0].Bars).Label);
        Assert.Equal("src/b.cs", Assert.Single(viewModel.Lanes[1].Bars).Label);
    }

    [Fact]
    public async Task ApplyParameter_WithDoneTaskButNoSelectedActivities_HasDataTrue_ButNoActivities()
    {
        var taskRepository = new FakeTaskRepository();
        var workSessionRepository = new FakeWorkSessionRepository();
        TaskItem task = TaskItem.Create("Tarefa sem atividades selecionadas");
        task.Start();
        task.Complete();
        await taskRepository.AddAsync(task, CancellationToken.None);

        var viewModel = CreateViewModel(taskRepository, workSessionRepository);
        viewModel.ApplyParameter(task.Id);
        await viewModel.LoadAsync();

        Assert.True(viewModel.HasData);
        Assert.False(viewModel.HasActivities);
        Assert.True(viewModel.NoActivities);
        Assert.Empty(viewModel.Lanes);
    }

    [Theory]
    [InlineData(false)] // ToDo
    [InlineData(true)] // InProgress
    public async Task ApplyParameter_WithTaskNotConcluded_IsUnavailable(bool started)
    {
        var taskRepository = new FakeTaskRepository();
        var workSessionRepository = new FakeWorkSessionRepository();
        TaskItem task = TaskItem.Create("Tarefa em andamento");
        if (started)
        {
            task.Start();
        }

        await taskRepository.AddAsync(task, CancellationToken.None);

        var viewModel = CreateViewModel(taskRepository, workSessionRepository);
        viewModel.ApplyParameter(task.Id);
        await viewModel.LoadAsync();

        Assert.True(viewModel.IsUnavailable);
        Assert.False(viewModel.HasData);
        Assert.False(string.IsNullOrEmpty(viewModel.UnavailableMessage));
    }

    [Fact]
    public async Task ApplyParameter_WithUnknownTaskId_IsUnavailable_DoesNotThrow()
    {
        var taskRepository = new FakeTaskRepository();
        var workSessionRepository = new FakeWorkSessionRepository();

        var viewModel = CreateViewModel(taskRepository, workSessionRepository);
        viewModel.ApplyParameter(Guid.NewGuid());
        await viewModel.LoadAsync();

        Assert.True(viewModel.IsUnavailable);
        Assert.False(viewModel.HasData);
    }

    [Fact]
    public void ApplyParameter_WithNonGuidParameter_IsIgnored()
    {
        var viewModel = CreateViewModel(new FakeTaskRepository(), new FakeWorkSessionRepository());

        viewModel.ApplyParameter("not-a-guid");

        Assert.False(viewModel.HasData);
        Assert.False(viewModel.IsUnavailable);
    }

    [Fact]
    public async Task ExportCsvCommand_WritesTheSameRowsAsCsvTaskActivityTimelineWriter()
    {
        var taskRepository = new FakeTaskRepository();
        var workSessionRepository = new FakeWorkSessionRepository();
        TaskItem task = await CreateDoneTaskWithActivitiesAsync(taskRepository, workSessionRepository);

        var saveDialog = new FakeReportFileSaveDialog();
        var viewModel = CreateViewModel(taskRepository, workSessionRepository, saveDialog);
        viewModel.ApplyParameter(task.Id);
        await viewModel.LoadAsync();

        viewModel.ExportCsvCommand.Execute(null);

        Assert.Equal(1, saveDialog.CallCount);
        Assert.Equal($"relatorio-tarefa-{task.Id}.csv", saveDialog.RequestedFileName);

        string? csv = saveDialog.CapturedCsv;
        Assert.NotNull(csv);
        Assert.Contains("arquivo,tempo_investido_segundos,inicio", csv);
        Assert.Contains("src/a.cs", csv);
        Assert.Contains("src/b.cs", csv);
    }

    [Fact]
    public async Task ExportCsvCommand_UserCancelsDialog_DoesNothing()
    {
        var taskRepository = new FakeTaskRepository();
        var workSessionRepository = new FakeWorkSessionRepository();
        TaskItem task = await CreateDoneTaskWithActivitiesAsync(taskRepository, workSessionRepository);

        var saveDialog = new FakeReportFileSaveDialog(cancels: true);
        var viewModel = CreateViewModel(taskRepository, workSessionRepository, saveDialog);
        viewModel.ApplyParameter(task.Id);
        await viewModel.LoadAsync();

        Exception? exception = Record.Exception(() => viewModel.ExportCsvCommand.Execute(null));

        Assert.Null(exception);
        Assert.Equal(1, saveDialog.CallCount);
        Assert.Null(saveDialog.CapturedCsv);
    }

    [Fact]
    public async Task ExportCsvCommand_WhenUnavailable_NeverOpensTheDialog()
    {
        var taskRepository = new FakeTaskRepository();
        var workSessionRepository = new FakeWorkSessionRepository();
        TaskItem task = TaskItem.Create("Tarefa ainda não concluída");
        await taskRepository.AddAsync(task, CancellationToken.None);

        var saveDialog = new FakeReportFileSaveDialog();
        var viewModel = CreateViewModel(taskRepository, workSessionRepository, saveDialog);
        viewModel.ApplyParameter(task.Id);
        await viewModel.LoadAsync();

        viewModel.ExportCsvCommand.Execute(null);

        Assert.Equal(0, saveDialog.CallCount);
    }

    [Fact]
    public async Task BackCommand_NavigatesToDetalhesTarefa_WithTaskId()
    {
        var taskRepository = new FakeTaskRepository();
        var workSessionRepository = new FakeWorkSessionRepository();
        TaskItem task = await CreateDoneTaskWithActivitiesAsync(taskRepository, workSessionRepository);

        var navigationService = new FakeNavigationService();
        var viewModel = CreateViewModel(taskRepository, workSessionRepository, navigationService: navigationService);
        viewModel.ApplyParameter(task.Id);
        await viewModel.LoadAsync();

        viewModel.BackCommand.Execute(null);

        (AppSection Section, object? Parameter) call = Assert.Single(navigationService.Calls);
        Assert.Equal(AppSection.DetalhesTarefa, call.Section);
        Assert.Equal(task.Id, call.Parameter);
    }
}
