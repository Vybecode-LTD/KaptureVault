using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Kapture.Models;
using Timer = System.Timers.Timer;

namespace Kapture.Services;

[SupportedOSPlatform("windows")]
public class ClipboardMonitorService : IClipboardMonitorService, IDisposable
{
    private const int PollIntervalMs = 500;
    private const int CF_UNICODETEXT = 13;

    // KV-005: derive from the running process (resolves to "KaptureVault" in the
    // published app) instead of a hardcoded name that drifted on rename.
    private static readonly string SelfProcessName = Process.GetCurrentProcess().ProcessName;

    private readonly IDatabaseService _db;
    private readonly IActiveWindowService _windowService;

    private Timer? _pollTimer;
    private uint _lastSequenceNumber;
    private string _lastClipboardText = string.Empty;
    private volatile bool _isPaused;
    private int _polling; // reentrancy guard

    public event Action? OnEntryFlushed;

    public ClipboardMonitorService(IDatabaseService db, IActiveWindowService windowService)
    {
        _db = db;
        _windowService = windowService;
    }

    public void Start()
    {
        _lastSequenceNumber = GetClipboardSequenceNumber();
        _lastClipboardText = ReadClipboardText() ?? string.Empty;

        _pollTimer = new Timer(PollIntervalMs);
        _pollTimer.Elapsed += PollClipboard;
        _pollTimer.Start();
    }

    public void Stop()
    {
        _pollTimer?.Stop();
    }

    public void Pause()
    {
        _isPaused = true;
    }

    public void Resume()
    {
        _isPaused = false;
        // Re-sync so we don't capture stale clipboard content on resume
        _lastSequenceNumber = GetClipboardSequenceNumber();
        _lastClipboardText = ReadClipboardText() ?? string.Empty;
    }

    private void PollClipboard(object? sender, System.Timers.ElapsedEventArgs e)
    {
        if (_isPaused) return;
        if (Interlocked.CompareExchange(ref _polling, 1, 0) != 0) return;

        try
        {
            var currentSeq = GetClipboardSequenceNumber();
            if (currentSeq == _lastSequenceNumber) return;

            var info = _windowService.GetActiveWindow();
            var appName = info?.ProcessName ?? "Unknown";
            var windowTitle = info?.WindowTitle ?? string.Empty;

            // Always consume the sequence number — even for self-originated changes.
            // Not consuming it causes the change to be captured later under the wrong app.
            _lastSequenceNumber = currentSeq;

            var text = ReadClipboardText();

            // KV-005 / KV-034: skip clipboard content that KaptureVault itself put on
            // the clipboard (Copy, Quick Paste), but still record it as the last-seen
            // text so the same string copied later from a real app isn't wrongly
            // suppressed by the dedupe check below.
            if (appName.Equals(SelfProcessName, StringComparison.OrdinalIgnoreCase))
            {
                if (!string.IsNullOrWhiteSpace(text))
                    _lastClipboardText = text;
                return;
            }

            if (string.IsNullOrWhiteSpace(text)) return;

            // Deduplicate: skip if identical to last captured text
            if (text == _lastClipboardText) return;
            _lastClipboardText = text;

            var entry = new CaptureEntry
            {
                AppName = appName,
                WindowTitle = windowTitle,
                Content = text,
                CharCount = text.Length,
                CapturedAt = DateTime.UtcNow,
                IsPinned = false,
                EntryType = "clipboard",
                DetectedLanguage = LanguageDetector.Detect(text)
            };

            _db.Insert(entry);
            OnEntryFlushed?.Invoke();
        }
        catch
        {
            // Silently fail — clipboard may be locked by another app
        }
        finally
        {
            Interlocked.Exchange(ref _polling, 0);
        }
    }

    private static string? ReadClipboardText()
    {
        if (!OpenClipboard(nint.Zero))
            return null;

        try
        {
            var hData = GetClipboardData(CF_UNICODETEXT);
            if (hData == nint.Zero) return null;

            var pData = GlobalLock(hData);
            if (pData == nint.Zero) return null;

            try
            {
                return Marshal.PtrToStringUni(pData);
            }
            finally
            {
                GlobalUnlock(hData);
            }
        }
        finally
        {
            CloseClipboard();
        }
    }

    public void Dispose()
    {
        Stop();
        _pollTimer?.Dispose();
        GC.SuppressFinalize(this);
    }

    // P/Invoke declarations
    [DllImport("user32.dll")]
    private static extern uint GetClipboardSequenceNumber();

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool OpenClipboard(nint hWndNewOwner);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseClipboard();

    [DllImport("user32.dll", SetLastError = true)]
    private static extern nint GetClipboardData(uint uFormat);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern nint GlobalLock(nint hMem);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalUnlock(nint hMem);
}
