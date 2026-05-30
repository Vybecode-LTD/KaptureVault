using System.Diagnostics;
using System.Text;
using System.Timers;
using Kapture.Models;
using Microsoft.Extensions.Logging;
using Timer = System.Timers.Timer;

namespace Kapture.Services;

public class CaptureService : ICaptureService, IDisposable
{
    private const int TickIntervalMs = 1_000;
    private const int WindowPollMs = 250;

    // KV-005: derive the self-exclusion name from the actual running process so a
    // future rename can't reintroduce the "captures its own input" drift. In the
    // published app this resolves to "KaptureVault".
    private static readonly string SelfProcessName = Process.GetCurrentProcess().ProcessName;

    private readonly IKeyboardHookService _hook;
    private readonly IActiveWindowService _windowService;
    private readonly IDatabaseService _db;
    private readonly ISettingsService? _settings;
    private readonly ILogger<CaptureService>? _log;

    private readonly StringBuilder _buffer = new();
    private readonly object _lock = new();

    // Settings-driven values (fall back to defaults if no settings service)
    private int MaxBufferSize => _settings?.Settings.MaxBufferChars ?? 5_000;
    private int IdleTimeoutMs => (_settings?.Settings.IdleFlushSeconds ?? 20) * 1_000;

    private string _currentApp = string.Empty;
    private string _currentTitle = string.Empty;
    private DateTime _lastKeystroke = DateTime.MinValue;
    private volatile bool _isPaused;
    private int _checkingIdle;   // Interlocked reentrancy guard
    private int _pollingWindow;  // Interlocked reentrancy guard

    private Timer? _idleTimer;
    private Timer? _windowPollTimer;

    public bool IsRecording => !_isPaused && _hook is not null;
    public event Action? OnEntryFlushed;

    public CaptureService(IKeyboardHookService hook, IActiveWindowService windowService, IDatabaseService db, ISettingsService? settings = null, ILogger<CaptureService>? log = null)
    {
        _hook = hook;
        _windowService = windowService;
        _db = db;
        _settings = settings;
        _log = log;
    }

    public void Start()
    {
        _hook.OnCharTyped += OnChar;
        _hook.OnBackspace += OnBackspace;
        _hook.OnEnter += OnEnter;
        _hook.OnTab += OnTab;
        _hook.Start();

        _idleTimer = new Timer(TickIntervalMs);
        _idleTimer.Elapsed += CheckIdle;
        _idleTimer.Start();

        _windowPollTimer = new Timer(WindowPollMs);
        _windowPollTimer.Elapsed += PollWindow;
        _windowPollTimer.Start();

        // Initialize current window
        var info = _windowService.GetActiveWindow();
        if (info != null)
        {
            _currentApp = info.ProcessName;
            _currentTitle = info.WindowTitle;
        }
    }

    public void Stop()
    {
        _idleTimer?.Stop();
        _windowPollTimer?.Stop();
        _hook.OnCharTyped -= OnChar;
        _hook.OnBackspace -= OnBackspace;
        _hook.OnEnter -= OnEnter;
        _hook.OnTab -= OnTab;
        _hook.Stop();
        Flush();
    }

    public void Pause()
    {
        _isPaused = true;
        Flush();
    }

    public void Resume()
    {
        _isPaused = false;
        // Re-acquire current window
        var info = _windowService.GetActiveWindow();
        if (info != null)
        {
            _currentApp = info.ProcessName;
            _currentTitle = info.WindowTitle;
        }
    }

    private void OnChar(char c)
    {
        if (_isPaused) return;
        lock (_lock)
        {
            _buffer.Append(c);
            _lastKeystroke = DateTime.UtcNow;
            if (_buffer.Length >= MaxBufferSize)
                Flush();
        }
    }

    private void OnBackspace()
    {
        if (_isPaused) return;
        lock (_lock)
        {
            if (_buffer.Length > 0)
                _buffer.Remove(_buffer.Length - 1, 1);
            _lastKeystroke = DateTime.UtcNow;
        }
    }

    private void OnEnter()
    {
        if (_isPaused) return;
        lock (_lock)
        {
            _buffer.Append('\n');
            _lastKeystroke = DateTime.UtcNow;
        }
    }

    private void OnTab()
    {
        if (_isPaused) return;
        lock (_lock)
        {
            _buffer.Append('\t');
            _lastKeystroke = DateTime.UtcNow;
        }
    }

    private void PollWindow(object? sender, ElapsedEventArgs e)
    {
        if (_isPaused) return;
        if (Interlocked.CompareExchange(ref _pollingWindow, 1, 0) != 0) return;
        try
        {
            var info = _windowService.GetActiveWindow();
            if (info == null) return;

            lock (_lock)
            {
                if (!info.ProcessName.Equals(_currentApp, StringComparison.OrdinalIgnoreCase))
                {
                    // App changed — flush the previous app's buffer as a completed session
                    Flush();
                    _currentApp = info.ProcessName;
                    _currentTitle = info.WindowTitle;
                }
                else if (!info.ProcessName.Equals(SelfProcessName, StringComparison.OrdinalIgnoreCase))
                {
                    // Same non-Kapture app — just update the window title for metadata
                    // (don't flush — title changes within the same app are normal)
                    _currentTitle = info.WindowTitle;
                }
                // If we're in Kapture itself, don't update tracking (self-exclusion)
            }
        }
        finally
        {
            Interlocked.Exchange(ref _pollingWindow, 0);
        }
    }

    private void CheckIdle(object? sender, ElapsedEventArgs e)
    {
        if (_isPaused) return;
        if (Interlocked.CompareExchange(ref _checkingIdle, 1, 0) != 0) return;
        try
        {
            lock (_lock)
            {
                if (_buffer.Length > 0 && _lastKeystroke != DateTime.MinValue &&
                    (DateTime.UtcNow - _lastKeystroke).TotalMilliseconds >= IdleTimeoutMs)
                {
                    Flush();
                }
            }
        }
        finally
        {
            Interlocked.Exchange(ref _checkingIdle, 0);
        }
    }

    private void Flush()
    {
        string content;
        string app;
        string title;

        lock (_lock)
        {
            if (_buffer.Length == 0) return;
            content = _buffer.ToString();
            app = _currentApp;
            title = _currentTitle;
            _buffer.Clear();
            _lastKeystroke = DateTime.MinValue;
        }

        if (string.IsNullOrWhiteSpace(content)) return;

        // Self-exclusion double-check
        if (app.Equals(SelfProcessName, StringComparison.OrdinalIgnoreCase))
            return;

        var entry = new CaptureEntry
        {
            AppName = app,
            WindowTitle = title,
            Content = content,
            CharCount = content.Length,
            CapturedAt = DateTime.UtcNow,
            IsPinned = false
        };

        try
        {
            _db.Insert(entry);
            OnEntryFlushed?.Invoke();
        }
        catch (Exception ex)
        {
            // Don't crash the hook thread, but log the data loss
            _log?.LogError(ex, "Failed to flush {CharCount} chars from {App} — data dropped", entry.CharCount, entry.AppName);
        }
    }

    public void Dispose()
    {
        Stop();
        _idleTimer?.Dispose();
        _windowPollTimer?.Dispose();
        GC.SuppressFinalize(this);
    }
}
