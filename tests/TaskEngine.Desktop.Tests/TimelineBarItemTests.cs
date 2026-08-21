using TaskEngine.Desktop.ViewModels;

namespace TaskEngine.Desktop.Tests;

/// <summary>
/// Direct tests for <see cref="TimelineBarItem"/>'s defensive clamping - the last line of defense
/// against a non-finite (NaN/Infinity) or out-of-[0,1] fraction ever reaching
/// <c>RelatorioPage.xaml</c>'s native <c>AbsoluteLayout.LayoutBounds</c> binding, regardless of
/// whether <c>RelatorioViewModel.RebuildLanes</c>'s own arithmetic stays airtight (see both types'
/// doc comments). <see cref="RelatorioViewModelTests"/> covers the same guard end-to-end through a
/// realistic disproportionate-duration scenario; these tests isolate the clamp itself against
/// values that arithmetic alone should never produce, but the constructor must survive regardless.
/// </summary>
public class TimelineBarItemTests
{
    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void Constructor_WithNonFiniteLeftFraction_FloorsToZeroInsteadOfPropagating(double invalidValue)
    {
        var bar = new TimelineBarItem("label", "1min", invalidValue, 0.5);

        Assert.Equal(0d, bar.LeftFraction);
        Assert.False(double.IsNaN(bar.LeftFraction));
        Assert.False(double.IsInfinity(bar.LeftFraction));
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void Constructor_WithNonFiniteWidthFraction_FloorsToMinVisibleWidthInsteadOfPropagating(double invalidValue)
    {
        var bar = new TimelineBarItem("label", "1min", 0.2, invalidValue);

        Assert.Equal(TimelineBarItem.MinVisibleWidthFraction, bar.WidthFraction);
        Assert.False(double.IsNaN(bar.WidthFraction));
        Assert.False(double.IsInfinity(bar.WidthFraction));
    }

    [Theory]
    [InlineData(-5d, 0d)]
    [InlineData(-0.001, 0d)]
    [InlineData(1.5, 1d)]
    [InlineData(50d, 1d)]
    public void Constructor_WithOutOfRangeLeftFraction_ClampsToZeroOne(double rawValue, double expected)
    {
        var bar = new TimelineBarItem("label", "1min", rawValue, 0.5);

        Assert.Equal(expected, bar.LeftFraction);
    }

    [Fact]
    public void Constructor_WithATinyButFiniteWidth_StillFloorsToMinVisibleWidth()
    {
        // A legitimately tiny (but finite) width - e.g. a very short activity relative to the
        // overall timeline span - gets the same "stay visible/tappable" floor as an invalid one,
        // matching RebuildLanes' pre-existing business rule (previously applied inline via
        // Math.Max, now centralized here).
        var bar = new TimelineBarItem("label", "1s", 0.5, 0.0001);

        Assert.Equal(TimelineBarItem.MinVisibleWidthFraction, bar.WidthFraction);
    }

    [Fact]
    public void Constructor_WithAWidthGreaterThanOne_ClampsToOne()
    {
        var bar = new TimelineBarItem("label", "1h", 0d, 3.7);

        Assert.Equal(1d, bar.WidthFraction);
    }

    [Fact]
    public void Constructor_WithNormalFractions_PassesThroughUnchanged()
    {
        var bar = new TimelineBarItem("src/a.cs", "10min", 0.25, 0.4);

        Assert.Equal(0.25, bar.LeftFraction);
        Assert.Equal(0.4, bar.WidthFraction);
    }
}
