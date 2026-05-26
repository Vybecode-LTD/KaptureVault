using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace Kapture.Services;

[SupportedOSPlatform("windows")]
public class KeyboardHookService : IKeyboardHookService, IDisposable
{
    private const int WH_KEYBOARD_LL = 13;
    private const int WM_KEYDOWN = 0x0100;

    public event Action<char>? OnCharTyped;
    public event Action? OnBackspace;
    public event Action? OnEnter;
    public event Action? OnTab;

    private nint _hookId;
    private readonly LowLevelKeyboardProc _proc;

    public KeyboardHookService()
    {
        // Store delegate in a readonly field so GC can never collect it
        _proc = HookCallback;
    }

    public void Start()
    {
        _hookId = SetWindowsHookEx(WH_KEYBOARD_LL, _proc, GetModuleHandle(null!), 0);
        if (_hookId == 0)
        {
            var err = Marshal.GetLastWin32Error();
            throw new InvalidOperationException(
                $"Failed to install keyboard hook. Win32 error: {err}");
        }
    }

    public void Stop()
    {
        if (_hookId != 0)
        {
            UnhookWindowsHookEx(_hookId);
            _hookId = 0;
        }
    }

    private nint HookCallback(int nCode, nint wParam, nint lParam)
    {
        if (nCode >= 0 && wParam == WM_KEYDOWN)
        {
            var hookStruct = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam);
            ProcessKey(hookStruct);
        }
        return CallNextHookEx(_hookId, nCode, wParam, lParam);
    }

    private void ProcessKey(KBDLLHOOKSTRUCT hookData)
    {
        int vkCode = (int)hookData.vkCode;

        // Check modifier state
        bool ctrlHeld = (GetAsyncKeyState(0x11) & 0x8000) != 0; // VK_CONTROL
        bool altHeld = (GetAsyncKeyState(0x12) & 0x8000) != 0;  // VK_MENU
        bool rightAlt = (GetAsyncKeyState(0xA5) & 0x8000) != 0; // VK_RMENU (AltGr)

        // AltGr registers as Ctrl+Alt — allow those through for character input
        bool isAltGr = ctrlHeld && altHeld && rightAlt;

        // Filter out Ctrl/Alt shortcuts, but allow AltGr combinations
        if ((ctrlHeld || altHeld) && !isAltGr)
            return;

        // Special keys
        switch (vkCode)
        {
            case 0x08: OnBackspace?.Invoke(); return; // VK_BACK
            case 0x0D: OnEnter?.Invoke(); return;     // VK_RETURN
            case 0x09: OnTab?.Invoke(); return;        // VK_TAB
        }

        // Ignore modifier keys, function keys, navigation keys, etc.
        if (vkCode is < 0x20                          // control chars
            or (>= 0x70 and <= 0x87)                  // F1-F24
            or (>= 0xA0 and <= 0xA5)                  // L/R Shift, Ctrl, Alt
            or 0x5B or 0x5C                            // Win keys
            or 0x1B                                    // Escape
            or (>= 0x21 and <= 0x28)                   // PgUp/PgDn/End/Home/Arrows
            or 0x2C or 0x2D or 0x2E                    // PrtSc/Insert/Delete
            or 0x90 or 0x91 or 0x14)                   // NumLock/ScrollLock/CapsLock
            return;

        // Get the foreground thread's keyboard layout for correct translation
        var foregroundWindow = GetForegroundWindow();
        var threadId = GetWindowThreadProcessId(foregroundWindow, out _);
        var layout = GetKeyboardLayout(threadId);

        // Capture the full keyboard state from the OS
        var keyState = new byte[256];
        GetKeyboardState(keyState);

        // Use the actual scan code from the hook struct, with extended flag
        uint scanCode = hookData.scanCode;
        if ((hookData.flags & 0x01) != 0) // LLKHF_EXTENDED
            scanCode |= 0x100;

        var buffer = new char[4];
        // wFlags=0x4: do not modify kernel dead-key state (Win10 1607+)
        int result = ToUnicodeEx(
            (uint)vkCode, scanCode, keyState,
            buffer, buffer.Length, 0x4, layout);

        if (result == 1 && !char.IsControl(buffer[0]))
        {
            OnCharTyped?.Invoke(buffer[0]);
        }
        else if (result >= 2)
        {
            // Multi-character output (e.g. dead key release)
            for (int i = 0; i < result; i++)
            {
                if (!char.IsControl(buffer[i]))
                    OnCharTyped?.Invoke(buffer[i]);
            }
        }
        // result == -1: dead key pressed, wFlags=0x4 prevents state corruption
        // result == 0: no translation (e.g. Shift alone) — ignore
    }

    public void Dispose()
    {
        Stop();
        GC.SuppressFinalize(this);
    }

    // Structs
    [StructLayout(LayoutKind.Sequential)]
    private struct KBDLLHOOKSTRUCT
    {
        public uint vkCode;
        public uint scanCode;
        public uint flags;
        public uint time;
        public nuint dwExtraInfo;
    }

    // Delegates
    private delegate nint LowLevelKeyboardProc(int nCode, nint wParam, nint lParam);

    // P/Invoke
    [DllImport("user32.dll", SetLastError = true)]
    private static extern nint SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, nint hMod, uint dwThreadId);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWindowsHookEx(nint hhk);

    [DllImport("user32.dll")]
    private static extern nint CallNextHookEx(nint hhk, int nCode, nint wParam, nint lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern nint GetModuleHandle(string? lpModuleName);

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int vKey);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetKeyboardState([Out] byte[] lpKeyState);

    [DllImport("user32.dll")]
    private static extern nint GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(nint hWnd, out uint processId);

    [DllImport("user32.dll")]
    private static extern nint GetKeyboardLayout(uint idThread);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int ToUnicodeEx(
        uint wVirtKey, uint wScanCode,
        [In] byte[] lpKeyState,
        [Out] char[] pwszBuff,
        int cchBuff, uint wFlags, nint dwhkl);
}
