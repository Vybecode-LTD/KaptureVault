using Kapture.Services;

namespace Kapture;

/// <summary>
/// T-08 (KV-011): the single, idempotent teardown for app exit. Stops the capture services and,
/// when enabled, runs a bounded sync-on-close. Extracted from <see cref="App"/> so the ordering /
/// gating / idempotency is unit-testable without an Avalonia lifetime. The tray icon and the
/// <see cref="System.IServiceProvider"/> (which disposes the IDisposable singletons HotkeyService
/// and CloudSyncManager — KV-024) are disposed by <see cref="App"/> after this completes.
/// </summary>
public sealed class ShutdownCoordinator
{
    private readonly ICaptureService _capture;
    private readonly IClipboardMonitorService _clipboard;
    private readonly IScreenshotService? _screenshot;
    private readonly ISettingsService _settings;
    private readonly Func<CancellationToken, Task>? _syncOnClose;
    private bool _done;

    /// <summary>True once <see cref="TeardownAsync"/> has run; the teardown is idempotent.</summary>
    public bool HasRun => _done;

    public ShutdownCoordinator(
        ICaptureService capture,
        IClipboardMonitorService clipboard,
        IScreenshotService? screenshot,
        ISettingsService settings,
        Func<CancellationToken, Task>? syncOnClose)
    {
        _capture = capture;
        _clipboard = clipboard;
        _screenshot = screenshot;
        _settings = settings;
        _syncOnClose = syncOnClose;
    }

    /// <summary>
    /// Stops capture, then (when <paramref name="runSyncOnClose"/> is true and the user left
    /// sync-on-close enabled) runs the sync-on-close delegate with a hard timeout. The delegate
    /// itself decides what actually syncs — P5 decouple: the Online Vault if signed in, the Google
    /// Drive backup if the user enabled it — so this only needs the SyncOnClose preference. Safe to
    /// call from any of the app's exit paths; only the first call does work.
    /// </summary>
    public async Task TeardownAsync(bool runSyncOnClose, TimeSpan syncTimeout)
    {
        if (_done) return;
        _done = true;

        Try(() => _capture.Stop());
        Try(() => _clipboard.Stop());
        Try(() => _screenshot?.Stop());

        if (runSyncOnClose
            && _syncOnClose is not null
            && _settings.Settings.SyncOnClose)
        {
            using var cts = new CancellationTokenSource(syncTimeout);
            try { await _syncOnClose(cts.Token); }
            catch { /* never block or crash shutdown on a sync failure */ }
        }
    }

    private static void Try(Action action)
    {
        try { action(); }
        catch { /* a single service failing to stop must not abort the rest of teardown */ }
    }
}
