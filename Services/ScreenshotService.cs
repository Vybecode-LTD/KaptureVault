using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Kapture.Models;
using Timer = System.Timers.Timer;

namespace Kapture.Services;

[SupportedOSPlatform("windows")]
public class ScreenshotService : IScreenshotService, IDisposable
{
    private const int PollIntervalMs = 500;
    private const int CF_DIB = 8;
    private const string SelfProcessName = "Kapture";

    private static readonly string ScreenshotDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".keystroke_vault", "screenshots");

    private readonly IDatabaseService _db;
    private readonly IActiveWindowService _windowService;

    private Timer? _pollTimer;
    private uint _lastSequenceNumber;
    private volatile bool _isPaused;
    private int _polling; // reentrancy guard

    public event Action? OnEntryFlushed;

    public ScreenshotService(IDatabaseService db, IActiveWindowService windowService)
    {
        _db = db;
        _windowService = windowService;
        Directory.CreateDirectory(ScreenshotDir);
    }

    public void Start()
    {
        _lastSequenceNumber = GetClipboardSequenceNumber();
        _pollTimer = new Timer(PollIntervalMs);
        _pollTimer.Elapsed += PollClipboardForImages;
        _pollTimer.Start();
    }

    public void Stop()
    {
        _pollTimer?.Stop();
    }

    public void Pause() => _isPaused = true;

    public void Resume()
    {
        _isPaused = false;
        _lastSequenceNumber = GetClipboardSequenceNumber();
    }

    private void PollClipboardForImages(object? sender, System.Timers.ElapsedEventArgs e)
    {
        if (_isPaused) return;
        if (Interlocked.CompareExchange(ref _polling, 1, 0) != 0) return;

        try
        {
            var currentSeq = GetClipboardSequenceNumber();
            if (currentSeq == _lastSequenceNumber) return;

            // Check if clipboard has an image (DIB format)
            if (!IsClipboardFormatAvailable(CF_DIB))
            {
                _lastSequenceNumber = currentSeq;
                return;
            }

            // Also check if there's text — if so, let ClipboardMonitorService handle it
            // We only capture images when there's NO text (pure image copy like PrtSc)
            if (IsClipboardFormatAvailable(13)) // CF_UNICODETEXT
            {
                // Text+image combo (e.g., copying from a webpage) — skip, clipboard monitor handles text
                _lastSequenceNumber = currentSeq;
                return;
            }

            var info = _windowService.GetActiveWindow();
            var appName = info?.ProcessName ?? "Unknown";
            var windowTitle = info?.WindowTitle ?? string.Empty;

            // Always consume sequence number — even for self-originated changes
            _lastSequenceNumber = currentSeq;

            if (appName.Equals(SelfProcessName, StringComparison.OrdinalIgnoreCase))
                return;

            // Extract the DIB from clipboard and save as BMP/PNG
            var imageBytes = ReadClipboardDib();
            if (imageBytes == null || imageBytes.Length == 0) return;

            var timestamp = DateTime.UtcNow;
            var filename = $"sc_{timestamp:yyyyMMdd_HHmmss_fff}.bmp";
            var filepath = Path.Combine(ScreenshotDir, filename);
            File.WriteAllBytes(filepath, imageBytes);

            var entry = new CaptureEntry
            {
                AppName = appName,
                WindowTitle = windowTitle,
                Content = filepath, // store file path
                CharCount = (int)(new FileInfo(filepath).Length / 1024), // size in KB
                CapturedAt = timestamp,
                IsPinned = false,
                EntryType = "screenshot"
            };

            _db.Insert(entry);
            OnEntryFlushed?.Invoke();
        }
        catch
        {
            // Silently fail — clipboard may be locked
        }
        finally
        {
            Interlocked.Exchange(ref _polling, 0);
        }
    }

    private static byte[]? ReadClipboardDib()
    {
        if (!OpenClipboard(nint.Zero))
            return null;

        try
        {
            var hData = GetClipboardData(CF_DIB);
            if (hData == nint.Zero) return null;

            // Use GlobalSize to get exact DIB size — avoids manual calculation bugs
            var totalDibSize = (int)GlobalSize(hData);
            if (totalDibSize <= 40) return null; // too small to be valid

            var pData = GlobalLock(hData);
            if (pData == nint.Zero) return null;

            try
            {
                // Read header fields for BMP file header offset calculation
                int headerSize = Marshal.ReadInt32(pData);        // biSize
                int bitCount = Marshal.ReadInt16(pData, 14);      // biBitCount
                int compression = Marshal.ReadInt32(pData, 16);   // biCompression
                int clrUsed = Marshal.ReadInt32(pData, 32);       // biClrUsed

                // Calculate color table / mask size for pixel data offset
                int extraSize = 0;
                if (compression == 3 || compression == 6) // BI_BITFIELDS or BI_ALPHABITFIELDS
                    extraSize = (compression == 6) ? 16 : 12; // 3 or 4 DWORD masks
                else if (bitCount <= 8)
                    extraSize = (clrUsed > 0 ? clrUsed : (1 << bitCount)) * 4;

                int pixelDataOffset = 14 + headerSize + extraSize;

                // Create BMP file: 14-byte file header + entire DIB
                int fileSize = 14 + totalDibSize;
                var bmpData = new byte[fileSize];

                // BMP file header
                bmpData[0] = (byte)'B';
                bmpData[1] = (byte)'M';
                BitConverter.GetBytes(fileSize).CopyTo(bmpData, 2);
                // bytes 6-9 reserved (zero)
                BitConverter.GetBytes(pixelDataOffset).CopyTo(bmpData, 10);

                // Copy entire DIB data (header + masks/color table + pixels)
                Marshal.Copy(pData, bmpData, 14, totalDibSize);

                return bmpData;
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

    // P/Invoke
    [DllImport("user32.dll")]
    private static extern uint GetClipboardSequenceNumber();

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsClipboardFormatAvailable(uint format);

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

    [DllImport("kernel32.dll")]
    private static extern nuint GlobalSize(nint hMem);
}
