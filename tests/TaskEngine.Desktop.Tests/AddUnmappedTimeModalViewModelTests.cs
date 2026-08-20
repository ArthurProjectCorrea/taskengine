using TaskEngine.Desktop.ViewModels;
using TaskEngine.Domain.Entities;

namespace TaskEngine.Desktop.Tests;

public class AddUnmappedTimeModalViewModelTests
{
    [Theory]
    [InlineData("1", "30", 90)]
    [InlineData("", "45", 45)]
    [InlineData("2", "", 120)]
    public void TryParseDuration_WithValidInputs_ReturnsExpectedTotalMinutes(string hours, string minutes, int expectedTotalMinutes)
    {
        var success = AddUnmappedTimeModalViewModel.TryParseDuration(hours, minutes, out TimeSpan duration, out var error);

        Assert.True(success);
        Assert.Null(error);
        Assert.Equal(expectedTotalMinutes, (int)duration.TotalMinutes);
    }

    [Theory]
    [InlineData("", "")]
    [InlineData("0", "0")]
    [InlineData("0", "")]
    public void TryParseDuration_WithZeroDuration_Fails(string hours, string minutes)
    {
        var success = AddUnmappedTimeModalViewModel.TryParseDuration(hours, minutes, out _, out var error);

        Assert.False(success);
        Assert.NotNull(error);
    }

    [Fact]
    public void TryParseDuration_WithNonNumericInput_Fails()
    {
        var success = AddUnmappedTimeModalViewModel.TryParseDuration("abc", "30", out _, out var error);

        Assert.False(success);
        Assert.NotNull(error);
    }

    [Fact]
    public async Task SaveCommand_WithValidData_PersistsEntry_ClosesModal_AndRaisesSaved()
    {
        var taskId = Guid.NewGuid();
        var repository = new FakeUnmappedTimeEntryRepository();
        var viewModel = new AddUnmappedTimeModalViewModel(repository);
        viewModel.Open(taskId);
        viewModel.HoursText = "1";
        viewModel.MinutesText = "15";
        viewModel.Justification = "Reunião presencial";

        Guid? raisedTaskId = null;
        viewModel.Saved += id => raisedTaskId = id;

        await viewModel.SaveCommand.ExecuteAsync(null);

        Assert.False(viewModel.IsOpen);
        Assert.Equal(taskId, raisedTaskId);

        IReadOnlyList<UnmappedTimeEntry> entries = await repository.ListByTaskIdAsync(taskId, CancellationToken.None);
        UnmappedTimeEntry entry = Assert.Single(entries);
        Assert.Equal(TimeSpan.FromMinutes(75), entry.Duration);
        Assert.Equal("Reunião presencial", entry.Justification);
    }

    [Fact]
    public async Task SaveCommand_WithoutJustification_ShowsError_WithoutPersisting()
    {
        // CA-006.2: missing justification blocks the record and must be requested back.
        var taskId = Guid.NewGuid();
        var repository = new FakeUnmappedTimeEntryRepository();
        var viewModel = new AddUnmappedTimeModalViewModel(repository);
        viewModel.Open(taskId);
        viewModel.HoursText = "1";
        viewModel.Justification = "   ";

        await viewModel.SaveCommand.ExecuteAsync(null);

        Assert.True(viewModel.IsOpen);
        Assert.True(viewModel.HasError);

        IReadOnlyList<UnmappedTimeEntry> entries = await repository.ListByTaskIdAsync(taskId, CancellationToken.None);
        Assert.Empty(entries);
    }

    [Fact]
    public async Task SaveCommand_WithZeroDuration_ShowsError_WithoutPersisting()
    {
        var taskId = Guid.NewGuid();
        var repository = new FakeUnmappedTimeEntryRepository();
        var viewModel = new AddUnmappedTimeModalViewModel(repository);
        viewModel.Open(taskId);
        viewModel.Justification = "Alguma justificativa";

        await viewModel.SaveCommand.ExecuteAsync(null);

        Assert.True(viewModel.IsOpen);
        Assert.True(viewModel.HasError);

        IReadOnlyList<UnmappedTimeEntry> entries = await repository.ListByTaskIdAsync(taskId, CancellationToken.None);
        Assert.Empty(entries);
    }

    [Fact]
    public void CancelCommand_ClosesTheModal()
    {
        var repository = new FakeUnmappedTimeEntryRepository();
        var viewModel = new AddUnmappedTimeModalViewModel(repository);
        viewModel.Open(Guid.NewGuid());

        viewModel.CancelCommand.Execute(null);

        Assert.False(viewModel.IsOpen);
    }
}
