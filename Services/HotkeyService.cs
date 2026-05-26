using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace Kapture.Services;

/// <summary>
/// Registers a global hotkey (Ctrl+Shift+V) using Windows RegisterHotKey API.
/// Fires OnHotkeyPressed when the hotkey is detected via a hidden message window.
/// </summary>
[SupportedOSPlatform("windows")]
public class HotkeyService : IDisposable
{
    private const int HOTKEY_ID = 0x4B50; // "KP" for Kapture
    private const int MOD_CONTROL = 0x0002;
    private const int MOD_SHIFT = 0x0004;
    private const int MOD_NOREPEAT = 0x4000;
    private const int VK_V = 0x56;
    private const int WM_HOTKEY = 0x0312;

    private nint _hwnd;
    private Thread? _messageThread;
    private volatile bool _running;
    private bool _registered;

    public event Action? OnHotkeyPressed;

    public void Start()
    {
        if (_running) return;
        _running = true;

        _messageThread = new Thread(MessageLoop)
        {
            IsBackground = true,
            Name = "HotkeyMessageLoop"
        };
        _messageThread.SetApartmentState(ApartmentState.STA);
        _messageThread.Start();
    }

    public void Stop()
    {
        _running = false;
        if (_hwnd != 0)
            PostMessage(_hwnd, 0x0012, 0, 0); // WM_QUIT
    }

    private void MessageLoop()
    {
        // Create a message-only window
        var className = "KaptureHotkeyClass_" + Environment.ProcessId;
        _wndProc = WndProc; // prevent GC

        var wc = new WNDCLASSEX
        {
            cbSize = (uint)Marshal.SizeOf<WNDCLASSEX>(),
            lpfnWndProc = Marshal.GetFunctionPointerForDelegate(_wndProc),
            hInstance = GetModuleHandle(null),
            lpszClassName = className
        };

        RegisterClassEx(ref wc);

        _hwnd = CreateWindowEx(0, className, "KaptureHotkey", 0,
            0, 0, 0, 0, new nint(-3) /* HWND_MESSAGE */, 0, GetModuleHandle(null), 0);

        if (_hwnd == 0) return;

        // Register Ctrl+Shift+V
        _registered = RegisterHotKey(_hwnd, HOTKEY_ID, MOD_CONTROL | MOD_SHIFT | MOD_NOREPEAT, VK_V);

        // Message pump
        while (_running && GetMessage(out var msg, 0, 0, 0) > 0)
        {
            TranslateMessage(ref msg);
            DispatchMessage(ref msg);
        }

        if (_registered)
            UnregisterHotKey(_hwnd, HOTKEY_ID);

        DestroyWindow(_hwnd);
        _hwnd = 0;
    }

    private WndProcDelegate? _wndProc;

    private nint WndProc(nint hwnd, uint msg, nint wParam, nint lParam)
    {
        if (msg == WM_HOTKEY && (int)wParam == HOTKEY_ID)
        {
            OnHotkeyPressed?.Invoke();
            return 0;
        }
        return DefWindowProc(hwnd, msg, wParam, lParam);
    }

    public void Dispose()
    {
        Stop();
        GC.SuppressFinalize(this);
    }

    // Delegates and P/Invoke
    private delegate nint WndProcDelegate(nint hwnd, uint msg, nint wParam, nint lParam);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WNDCLASSEX
    {
        public uint cbSize;
        public uint style;
        public nint lpfnWndProc;
        public int cbClsExtra;
        public int cbWndExtra;
        public nint hInstance;
        public nint hIcon;
        public nint hCursor;
        public nint hbrBackground;
        [MarshalAs(UnmanagedType.LPWStr)] public string lpszMenuName;
        [MarshalAs(UnmanagedType.LPWStr)] public string lpszClassName;
        public nint hIconSm;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MSG
    {
        public nint hwnd;
        public uint message;
        public nint wParam;
        public nint lParam;
        public uint time;
        public int pt_x;
        public int pt_y;
    }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool RegisterHotKey(nint hWnd, int id, int fsModifiers, int vk);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnregisterHotKey(nint hWnd, int id);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern ushort RegisterClassEx(ref WNDCLASSEX lpwcx);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern nint CreateWindowEx(uint dwExStyle, string lpClassName, string lpWindowName,
        uint dwStyle, int x, int y, int nWidth, int nHeight, nint hWndParent, nint hMenu, nint hInstance, nint lpParam);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyWindow(nint hWnd);

    [DllImport("user32.dll")]
    private static extern int GetMessage(out MSG lpMsg, nint hWnd, uint wMsgFilterMin, uint wMsgFilterMax);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool TranslateMessage(ref MSG lpMsg);

    [DllImport("user32.dll")]
    private static extern nint DispatchMessage(ref MSG lpMsg);

    [DllImport("user32.dll")]
    private static extern nint DefWindowProc(nint hWnd, uint msg, nint wParam, nint lParam);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool PostMessage(nint hWnd, uint msg, nint wParam, nint lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern nint GetModuleHandle(string? lpModuleName);
}
