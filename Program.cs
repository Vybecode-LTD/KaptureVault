using Avalonia;
using System;
using System.Threading;

namespace Kapture;

sealed class Program
{
    private static Mutex? _mutex;

    [STAThread]
    public static int Main(string[] args)
    {
        const string mutexName = "Global\\KaptureVault_SingleInstance_C9D2E5F6";
        _mutex = new Mutex(true, mutexName, out bool createdNew);

        if (!createdNew)
            return 0;

        try
        {
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
            return 0;
        }
        finally
        {
            _mutex.ReleaseMutex();
            _mutex.Dispose();
        }
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
