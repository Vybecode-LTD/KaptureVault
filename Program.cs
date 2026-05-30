using Avalonia;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Security.Principal;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Threading;

namespace Kapture;

sealed class Program
{
    private static Mutex? _mutex;

    [STAThread]
    public static int Main(string[] args)
    {
        // ── Elevation check (Capture Admin Apps) ─────────────────────────────
        // Must run before mutex acquisition so that, when we relaunch elevated,
        // the new elevated process can acquire the mutex without racing this one.
        if (CheckAndHandleElevation(out int earlyExit))
            return earlyExit;

        // ── Single-instance mutex ─────────────────────────────────────────────
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
            try { _mutex?.ReleaseMutex(); } catch { }
            _mutex?.Dispose();
        }
    }

    /// <summary>
    /// Releases the single-instance mutex early so a restarted (elevated or
    /// de-elevated) process can acquire it without waiting for this process to exit.
    /// Must be called immediately before launching the replacement process.
    /// </summary>
    internal static void PrepareForRestart()
    {
        try { _mutex?.ReleaseMutex(); } catch { }
        _mutex?.Dispose();
        _mutex = null;
    }

    /// <summary>Returns true when running as a member of the Administrators group.</summary>
    internal static bool IsRunningAsAdmin()
    {
        try
        {
            using var identity = WindowsIdentity.GetCurrent();
            return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
        }
        catch { return false; }
    }

    // Returns true when Main should exit immediately (elevated process is launching).
    private static bool CheckAndHandleElevation(out int exitCode)
    {
        exitCode = 0;

        if (IsRunningAsAdmin())
            return false; // already elevated — normal startup

        var settings = TryLoadSettings();
        if (settings?.CaptureAdminApps != true)
            return false; // elevation not requested — normal startup

        // CaptureAdminApps = true but we're at standard-user integrity — relaunch with UAC.
        try
        {
            Process.Start(new ProcessStartInfo(Environment.ProcessPath!)
            {
                UseShellExecute = true,
                Verb = "runas"
            });
            return true; // elevated instance is starting; exit this one
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == 1223) // ERROR_CANCELLED — UAC denied
        {
            // User clicked "No" on the UAC prompt — revert the setting so the next
            // launch doesn't loop back into here.
            if (settings != null)
            {
                settings.CaptureAdminApps = false;
                TrySaveSettings(settings);
            }
            return false; // continue as non-elevated
        }
        catch
        {
            return false; // any other error — continue normally
        }
    }

    // ── Lightweight settings I/O (no DI, no full model needed) ───────────────

    private static MinimalSettings? TryLoadSettings()
    {
        try
        {
            var path = SettingsFilePath();
            if (!File.Exists(path)) return null;
            return JsonSerializer.Deserialize<MinimalSettings>(
                File.ReadAllText(path),
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch { return null; }
    }

    // Patches only captureAdminApps in the JSON file, leaving all other keys intact.
    private static void TrySaveSettings(MinimalSettings settings)
    {
        try
        {
            var path = SettingsFilePath();
            if (!File.Exists(path)) return;
            var node = JsonNode.Parse(File.ReadAllText(path))!;
            node["captureAdminApps"] = settings.CaptureAdminApps;
            File.WriteAllText(path,
                node.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
        }
        catch { /* best-effort */ }
    }

    private static string SettingsFilePath() =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "KaptureVault", "settings.json");

    // Mirrors only the field(s) needed at startup time.
    private sealed class MinimalSettings
    {
        [JsonPropertyName("captureAdminApps")]
        public bool CaptureAdminApps { get; set; }
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
