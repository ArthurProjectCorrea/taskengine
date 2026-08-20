using TaskEngine.Domain.Entities;
using TaskEngine.Domain.Monitoring;

namespace TaskEngine.Domain.Tests.Monitoring;

public class ActivitySourceClassifierTests
{
    [Theory]
    [InlineData("claude")]
    [InlineData("Claude")]
    [InlineData("claude.exe")]
    [InlineData("copilot")]
    [InlineData("github-copilot")]
    [InlineData("cursor")]
    [InlineData("aider")]
    [InlineData("codex")]
    [InlineData("windsurf")]
    [InlineData("amazonq")]
    [InlineData("continue")]
    [InlineData("cody")]
    public void Classify_KnownAiProcessNames_ReturnsAi(string processName)
    {
        Assert.Equal(ActivitySource.Ai, ActivitySourceClassifier.Classify(processName));
    }

    [Theory]
    [InlineData("explorer")]
    [InlineData("chrome")]
    [InlineData("devenv")]
    [InlineData("notepad")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Classify_UnknownOrEmptyProcessNames_ReturnsHuman(string? processName)
    {
        Assert.Equal(ActivitySource.Human, ActivitySourceClassifier.Classify(processName));
    }

    [Fact]
    public void ClassifyFromRunningProcesses_WhenAnAiProcessIsRunning_ReturnsAi()
    {
        var runningProcesses = new[] { "explorer", "chrome", "claude" };

        Assert.Equal(ActivitySource.Ai, ActivitySourceClassifier.ClassifyFromRunningProcesses(runningProcesses));
    }

    [Fact]
    public void ClassifyFromRunningProcesses_WhenNoAiProcessIsRunning_ReturnsHuman()
    {
        var runningProcesses = new[] { "explorer", "chrome", "devenv" };

        Assert.Equal(ActivitySource.Human, ActivitySourceClassifier.ClassifyFromRunningProcesses(runningProcesses));
    }

    [Fact]
    public void ClassifyFromRunningProcesses_WithNoProcesses_ReturnsHuman()
    {
        Assert.Equal(ActivitySource.Human, ActivitySourceClassifier.ClassifyFromRunningProcesses([]));
    }
}
