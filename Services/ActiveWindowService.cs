using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;

namespace Kapture.Services;

[SupportedOSPlatform("windows")]
public class ActiveWindowService : IActiveWindowService
{
    public ActiveWindowInfo? GetActiveWindow()
    {
        var hwnd = GetForegroundWindow();
        if (hwnd == 0)
            return null;

        // Get window title
        var titleBuilder = new StringBuilder(256);
        GetWindowText(hwnd, titleBuilder, titleBuilder.Capacity);
        var title = titleBuilder.ToString();

        // Get process name
        GetWindowThreadProcessId(hwnd, out uint processId);
        try
        {
            using var process = Process.GetProcessById((int)processId);
            return new ActiveWindowInfo(process.ProcessName, title);
        }
        catch
        {
            return null;
        }
    }

    [DllImport("user32.dll")]
    private static extern nint GetForegroundWindow();

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowText(nint hWnd, StringBuilder lpString, int nMaxCount);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(nint hWnd, out uint lpdwProcessId);
}
