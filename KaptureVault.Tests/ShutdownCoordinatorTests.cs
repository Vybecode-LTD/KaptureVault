using FluentAssertions;
using Kapture;
using Kapture.Models;
using Kapture.Services;
using NSubstitute;
using Xunit;

namespace KaptureVault.Tests;

/// <summary>
/// T-08 (KV-011): the centralized teardown stops capture, gates sync-on-close on both the user's
/// settings and the caller's intent (restart paths pass false), swallows sync failures, and is
/// idempotent across the app's several exit paths. (Disposing the tray + ServiceProvider lives in
/// App and runs after this; the ServiceProvider disposes HotkeyService + CloudSyncManager — KV-024.)
/// </summary>
public class ShutdownCoordinatorTests
{
    private static (ShutdownCoordinator coord, ICaptureService cap, IClipboardMonitorService clip, IScreenshotService shot) Build(
        bool cloudSync = true, bool syncOnClose = true, Func<CancellationToken, Task>? sync = null)
    {
        var cap = Substitute.For<ICaptureService>();
        var clip = Substitute.For<IClipboardMonitorService>();
        var shot = Substitute.For<IScreenshotService>();
        var settings = Substitute.For<ISettingsService>();
        settings.Settings.Returns(new AppSettings { CloudSyncEnabled = cloudSync, SyncOnClose = syncOnClose });
        return (new ShutdownCoordinator(cap, clip, shot, settings, sync), cap, clip, shot);
    }

    [Fact]
    public async Task Teardown_StopsAllCaptureServices()
    {
        var (coord, cap, clip, shot) = Build(cloudSync: false);

        await coord.TeardownAsync(runSyncOnClose: true, TimeSpan.FromSeconds(1));

        cap.Received(1).Stop();
        clip.Received(1).Stop();
        shot.Received(1).Stop();
        coord.HasRun.Should().BeTrue();
    }

    [Fact]
    public async Task Teardown_IsIdempotent_StopsOnlyOnce()
    {
        var (coord, cap, clip, shot) = Build(cloudSync: false);

        await coord.TeardownAsync(true, TimeSpan.FromSeconds(1));
        await coord.TeardownAsync(true, TimeSpan.FromSeconds(1));

        cap.Received(1).Stop();
        clip.Received(1).Stop();
        shot.Received(1).Stop();
    }

    [Fact]
    public async Task Teardown_RunsSyncOnClose_WhenEnabledAndRequested()
    {
        var syncCount = 0;
        var (coord, _, _, _) = Build(cloudSync: true, syncOnClose: true,
            sync: _ => { syncCount++; return Task.CompletedTask; });

        await coord.TeardownAsync(runSyncOnClose: true, TimeSpan.FromSeconds(1));

        syncCount.Should().Be(1);
    }

    [Fact]
    public async Task Teardown_SkipsSync_WhenCallerDisablesIt()
    {
        // The restart paths pass runSyncOnClose: false — a restart is not a quit.
        var syncCount = 0;
        var (coord, _, _, _) = Build(cloudSync: true, syncOnClose: true,
            sync: _ => { syncCount++; return Task.CompletedTask; });

        await coord.TeardownAsync(runSyncOnClose: false, TimeSpan.FromSeconds(1));

        syncCount.Should().Be(0);
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(false, false)]
    public async Task Teardown_SkipsSync_WhenSettingsDisableIt(bool cloudSync, bool syncOnClose)
    {
        var syncCount = 0;
        var (coord, _, _, _) = Build(cloudSync, syncOnClose,
            sync: _ => { syncCount++; return Task.CompletedTask; });

        await coord.TeardownAsync(runSyncOnClose: true, TimeSpan.FromSeconds(1));

        syncCount.Should().Be(0);
    }

    [Fact]
    public async Task Teardown_StillStopsCapture_AndDoesNotThrow_WhenSyncThrows()
    {
        var (coord, cap, _, _) = Build(cloudSync: true, syncOnClose: true,
            sync: _ => throw new InvalidOperationException("sync boom"));

        var act = async () => await coord.TeardownAsync(true, TimeSpan.FromSeconds(1));

        await act.Should().NotThrowAsync();
        cap.Received(1).Stop();
    }
}
