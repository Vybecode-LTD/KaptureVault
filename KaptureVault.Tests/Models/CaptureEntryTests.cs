using FluentAssertions;
using Kapture.Models;
using Xunit;

namespace KaptureVault.Tests.Models;

/// <summary>
/// Phase 3 slice G: <see cref="CaptureEntry.ScreenshotPath"/> resolve-by-filename fallback. A row
/// whose stored Content path was captured on another device must still resolve to the locally-restored
/// image (written into <see cref="CaptureEntry.ScreenshotDirectory"/> keyed by filename).
/// </summary>
[Collection("ScreenshotDirectory")] // serialized with ScreenshotSyncServiceTests: both mutate the static ScreenshotDirectory
public sealed class CaptureEntryTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), $"kv-ce-{Guid.NewGuid():N}");
    private readonly string _originalScreenshotDir = CaptureEntry.ScreenshotDirectory;

    public CaptureEntryTests() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        CaptureEntry.ScreenshotDirectory = _originalScreenshotDir;
        try { Directory.Delete(_dir, recursive: true); } catch { /* best effort */ }
    }

    [Fact]
    public void ScreenshotPath_ReturnsStoredPath_WhenFileExistsThere()
    {
        var path = Path.Combine(_dir, "sc_here.bmp");
        File.WriteAllBytes(path, [1]);
        var e = new CaptureEntry { EntryType = "screenshot", Content = path };

        e.ScreenshotPath.Should().Be(path);
    }

    [Fact]
    public void ScreenshotPath_FallsBackToScreenshotDirectoryByFilename_WhenStoredPathMissing()
    {
        CaptureEntry.ScreenshotDirectory = _dir;
        var local = Path.Combine(_dir, "sc_restored.bmp");
        File.WriteAllBytes(local, [1]);
        // Content is another device's absolute path — does not exist here, but the filename matches.
        var e = new CaptureEntry { EntryType = "screenshot", Content = @"C:\other-device\users\bob\sc_restored.bmp" };

        e.ScreenshotPath.Should().Be(local);
    }

    [Fact]
    public void ScreenshotPath_Null_WhenNeitherStoredNorLocalExists()
    {
        CaptureEntry.ScreenshotDirectory = _dir;
        var e = new CaptureEntry { EntryType = "screenshot", Content = @"C:\nope\sc_x.bmp" };

        e.ScreenshotPath.Should().BeNull();
    }

    [Fact]
    public void ScreenshotPath_Null_ForNonScreenshotEntry()
    {
        var e = new CaptureEntry { EntryType = "keyboard", Content = "just some captured text" };

        e.ScreenshotPath.Should().BeNull();
    }
}
