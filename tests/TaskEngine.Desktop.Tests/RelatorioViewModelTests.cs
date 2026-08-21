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

    /// <summary>
    /// Reproduction attempt for the reported "abrir Relatório crasha o app inteiro" bug. The lead
    /// hypothesis: a task completed before the <c>WorkSession.AttributeSelectedActivity</c> clipping
    /// fix (commit 2fd6e15) could have a selected activity whose duration is wildly disproportionate
    /// to its work session (e.g. a file monitored for 5 days, selected into a 2h session) - and
    /// RebuildLanes' left/width fraction math might turn that into a NaN/Infinity/out-of-range value
    /// that crashes the native <c>AbsoluteLayout.LayoutBounds</c> binding.
    /// <para/>
    /// This test simulates exactly that persisted shape directly via <see cref="WorkSession.RecordActivity"/>
    /// (which - unlike the now-fixed <c>AttributeSelectedActivity</c> - has never clipped to the
    /// session's own window, so it faithfully reproduces the pre-fix stored data). The result:
    /// every produced <see cref="TimelineBarItem"/> fraction stays finite and within [0, 1] even
    /// here - RebuildLanes' overallStart/overallEnd are derived from the very same rows being
    /// positioned, so the arithmetic is bounded by construction (see RebuildLanes' own doc comment
    /// for the proof). That does NOT mean the crash report is wrong - it means the root cause, if
    /// this same disproportionate-duration shape is truly involved, is not a bare division producing
    /// a non-finite fraction. Either way, <see cref="TimelineBarItem"/>'s constructor now clamps
    /// defensively regardless (see <c>TimelineBarItemTests</c>), so this screen can no longer pass a
    /// non-finite value to the native binding under any input.
    /// </summary>
    [Fact]
    public async Task ApplyParameter_WithDisproportionatelyLongSelectedActivity_ProducesOnlyFiniteBoundedFractions()
    {
        var taskRepository = new FakeTaskRepository();
        var workSessionRepository = new FakeWorkSessionRepository();
        TaskItem task = TaskItem.Create("Tarefa com atividade de duração desproporcional");
        task.Start();
        await taskRepository.AddAsync(task, CancellationToken.None);

        var sessionStart = new DateTimeOffset(2026, 1, 10, 9, 0, 0, TimeSpan.Zero);
        WorkSession session = WorkSession.Start(task.Id, sessionStart);
        // Unclipped, pre-fix shape: this activity's own interval starts 5 days before the session
        // that "selected" it even began.
        session.RecordActivity(ActivitySource.Human, sessionStart.AddDays(-5), sessionStart.AddHours(1), ActivityItemType.File, "src/huge.cs");
        session.RecordActivity(ActivitySource.Human, sessionStart.AddHours(1), sessionStart.AddHours(2), ActivityItemType.File, "src/normal.cs");
        session.ApplyActivitySelection(new HashSet<Guid> { session.Activities[0].Id, session.Activities[1].Id });
        session.End(sessionStart.AddHours(2));
        await workSessionRepository.AddAsync(session, CancellationToken.None);

        task.Complete();
        await taskRepository.UpdateAsync(task, CancellationToken.None);

        var viewModel = CreateViewModel(taskRepository, workSessionRepository);
        viewModel.ApplyParameter(task.Id);
        await viewModel.LoadAsync();

        Assert.True(viewModel.HasData);
        List<TimelineBarItem> allBars = viewModel.Lanes.SelectMany(l => l.Bars).ToList();
        Assert.NotEmpty(allBars);

        foreach (TimelineBarItem bar in allBars)
        {
            Assert.False(double.IsNaN(bar.LeftFraction), "LeftFraction must never be NaN.");
            Assert.False(double.IsInfinity(bar.LeftFraction), "LeftFraction must never be Infinity.");
            Assert.False(double.IsNaN(bar.WidthFraction), "WidthFraction must never be NaN.");
            Assert.False(double.IsInfinity(bar.WidthFraction), "WidthFraction must never be Infinity.");
            Assert.InRange(bar.LeftFraction, 0d, 1d);
            Assert.InRange(bar.WidthFraction, TimelineBarItem.MinVisibleWidthFraction, 1d);
        }
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
