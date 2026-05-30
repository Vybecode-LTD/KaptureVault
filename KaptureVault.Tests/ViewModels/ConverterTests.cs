using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;
using FluentAssertions;
using Kapture.ViewModels;
using Xunit;

namespace KaptureVault.Tests.ViewModels;

/// <summary>
/// Covers the static value converters exposed on <see cref="MainWindowViewModel"/>.
/// Includes KV-033: the per-row brush converters must return shared cached instances
/// (no per-call <c>SolidColorBrush</c> allocation).
/// </summary>
public class ConverterTests
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    private static object? Run(IValueConverter c, object? value, object? param = null)
        => c.Convert(value, typeof(object), param, Inv);

    // ── KV-033: brush converters return cached, shared instances ──────────────

    [Theory]
    [InlineData("clipboard", "#D2A8FF")]
    [InlineData("screenshot", "#58A6FF")]
    [InlineData("keyboard", "#3FB950")]
    [InlineData("anything-else", "#3FB950")]
    public void EntryTypeColor_ReturnsExpectedColor(string type, string hex)
    {
        var brush = Run(MainWindowViewModel.EntryTypeColorConverter, type)
            .Should().BeAssignableTo<ISolidColorBrush>().Subject;
        brush.Color.Should().Be(Color.Parse(hex));
    }

    [Fact]
    public void EntryTypeColor_ReturnsSameInstanceForSameType_AndDistinctForDifferent()
    {
        var c = MainWindowViewModel.EntryTypeColorConverter;
        Run(c, "clipboard").Should().BeSameAs(Run(c, "clipboard")); // cached, not re-allocated
        Run(c, "clipboard").Should().NotBeSameAs(Run(c, "keyboard"));
    }

    [Theory]
    [InlineData(4900, "#F85149")] // >0.8 ratio → red
    [InlineData(3000, "#D29922")] // >0.5 ratio → yellow
    [InlineData(100, "#3FB950")]  // low → green
    public void BufferFillColor_ReturnsExpected_AndIsCached(int charCount, string hex)
    {
        var c = MainWindowViewModel.BufferFillColorConverter;
        var brush = Run(c, charCount).Should().BeAssignableTo<ISolidColorBrush>().Subject;
        brush.Color.Should().Be(Color.Parse(hex));
        Run(c, charCount).Should().BeSameAs(brush);
    }

    // ── Pure text / number converters (T-16 coverage) ─────────────────────────

    [Fact]
    public void Preview_TruncatesAndFlattensNewlines()
    {
        Run(MainWindowViewModel.PreviewConverter, "hello\nworld").Should().Be("hello world");
        var long201 = new string('x', 201);
        var result = (string)Run(MainWindowViewModel.PreviewConverter, long201)!;
        result.Should().HaveLength(83).And.EndWith("...");
    }

    [Theory]
    [InlineData("clipboard", "CB")]
    [InlineData("screenshot", "SC")]
    [InlineData("keyboard", "KB")]
    public void EntryTypeIcon_MapsType(string type, string icon)
        => Run(MainWindowViewModel.EntryTypeIconConverter, type).Should().Be(icon);

    [Theory]
    [InlineData(true, "Unpin")]
    [InlineData(false, "Pin")]
    public void PinLabel_MapsState(bool pinned, string label)
        => Run(MainWindowViewModel.PinLabelConverter, pinned).Should().Be(label);

    [Fact]
    public void TypeFilterActive_FullOpacityWhenSelected()
    {
        Run(MainWindowViewModel.TypeFilterActiveConverter, "Keyboard", "Keyboard").Should().Be(1.0);
        Run(MainWindowViewModel.TypeFilterActiveConverter, "Keyboard", "Clipboard").Should().Be(0.4);
    }

    [Fact]
    public void BufferFillWidth_ScalesWithRatio_WithMinimum()
    {
        Run(MainWindowViewModel.BufferFillWidthConverter, 0).Should().Be(2.0);     // min 2px
        Run(MainWindowViewModel.BufferFillWidthConverter, 5000).Should().Be(200.0); // full bar
        ((double)Run(MainWindowViewModel.BufferFillWidthConverter, 2500)!).Should().BeApproximately(100.0, 0.01);
    }
}
