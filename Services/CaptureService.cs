using System.Diagnostics;
using System.Text;
using System.Threading.Channels;
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

    // KV-012/T-07: the keyboard-hook callback (and the poll timers) must never block on a
    // SQLite write. Flush() hands the built entry to this bounded queue; a single writer
    // task performs Open()+INSERT+AES off the hook thread. Bounded + non-blocking TryWrite
    // means a stalled DB can never back-pressure into the WH_KEYBOARD_LL callback.
    private const int WriteQueueCapacity = 1024;
    private Channel<CaptureEntry>? _writeQueue;
    private Task? _writerTask;

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
        // Spin up the write pipeline before any keystroke can arrive (KV-012/T-07).
        _writeQueue = Channel.CreateBounded<CaptureEntry>(new BoundedChannelOptions(WriteQueueCapacity)
        {
            SingleReader = true,
            SingleWriter = false, // Flush() is reached from the hook + both poll timers
            FullMode = BoundedChannelFullMode.Wait, // TryWrite returns false (never blocks) when full
            AllowSynchronousContinuations = false, // never run the writer inline on the producer (hook) thread
        });
        _writerTask = Task.Run(() => ProcessWriteQueueAsync(_writeQueue.Reader));

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

        // Drain the write queue so a shutdown doesn't drop the final buffered entry.
        // Completing the writer lets the loop finish naturally; the bounded wait keeps
        // shutdown from hanging if the DB is wedged (e.g. mid sync-replace).
        _writeQueue?.Writer.TryComplete();
        try { _writerTask?.Wait(TimeSpan.FromSeconds(5)); }
        catch (AggregateException) { /* per-item write failures are already logged */ }
        _writerTask = null;
        _writeQueue = null;
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

        // KV-012/T-07: hand off to the writer task — never touch SQLite on this thread,
        // which may be the WH_KEYBOARD_LL hook callback. TryWrite never blocks; if the
        // queue is somehow saturated we drop (and log) rather than stall input.
        var queue = _writeQueue;
        if (queue is null)
            return; // not running (Start() hasn't created the pipeline)

        if (!queue.Writer.TryWrite(entry))
            _log?.LogWarning("Capture write queue full — dropped {CharCount} chars from {App}", entry.CharCount, entry.AppName);
    }

    // KV-012/T-07: the single consumer of the write queue. Runs on a Task (thread-pool),
    // so the Open()+INSERT+AES cost is paid here, never on the hook/timer threads.
    private async Task ProcessWriteQueueAsync(ChannelReader<CaptureEntry> reader)
    {
        await foreach (var entry in reader.ReadAllAsync().ConfigureAwait(false))
        {
            try
            {
                _db.Insert(entry);
                OnEntryFlushed?.Invoke();
            }
            catch (Exception ex)
            {
                // One failed write must not tear down the writer; log the data loss.
                _log?.LogError(ex, "Failed to persist {CharCount} chars from {App} — data dropped", entry.CharCount, entry.AppName);
            }
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
