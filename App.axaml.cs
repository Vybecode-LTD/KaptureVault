using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core.Plugins;
using Avalonia.Markup.Xaml;
using Avalonia.Platform;
using Avalonia.Styling;
using Kapture.Services;
using Kapture.Services.CloudSync;
using Kapture.ViewModels;
using Kapture.Views;
using Microsoft.Extensions.DependencyInjection;

namespace Kapture;

public partial class App : Application
{
    private static ServiceProvider? _serviceProvider;
    public static IServiceProvider Services => _serviceProvider!;

    private ICaptureService? _capture;
    private IClipboardMonitorService? _clipboardMonitor;
    private IScreenshotService? _screenshotService;
    private HotkeyService? _hotkeyService;
    private MainWindow? _mainWindow;
    private TrayIcon? _trayIcon;
    private bool _quickPasteOpen;

    // T-08 (KV-011/KV-024): single idempotent teardown wired to ShutdownRequested.
    private ShutdownCoordinator? _shutdown;
    private bool _teardownDone;
    private bool _syncOnShutdown = true;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override async void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            DisableAvaloniaDataAnnotationValidation();

            // DI setup
            var services = new ServiceCollection();
            services.AddKaptureServices();
            _serviceProvider = services.BuildServiceProvider();


            // Load settings first
            var settingsService = _serviceProvider.GetRequiredService<ISettingsService>();
            settingsService.Load();

            // Apply saved theme
            ApplyTheme(settingsService.Settings.Theme);

            // Listen for settings changes to re-apply theme
            settingsService.OnSettingsChanged += () => ApplyTheme(settingsService.Settings.Theme);

            // Handle encryption unlock if configured
            var encryptionService = _serviceProvider.GetRequiredService<IEncryptionService>();
            if (encryptionService.IsConfigured)
            {
                // Show unlock dialog — keep asking until correct or user cancels
                var unlocked = false;
                while (!unlocked)
                {
                    var dialog = new Views.Dialogs.PasswordDialog(Views.Dialogs.PasswordDialog.DialogMode.Unlock);
                    // We need a temp window as owner for the dialog
                    var tempWindow = new Window { Width = 0, Height = 0, WindowState = WindowState.Minimized, ShowInTaskbar = false };
                    tempWindow.Show();
                    await dialog.ShowDialog(tempWindow);
                    tempWindow.Close();

                    if (!dialog.WasConfirmed)
                    {
                        // User cancelled — dispose the container we built (no services started
                        // yet), then quit. ShutdownRequested isn't wired this early, so dispose here.
                        (_serviceProvider as IDisposable)?.Dispose();
                        desktop.Shutdown();
                        return;
                    }

                    unlocked = encryptionService.Unlock(dialog.ResultPassword!);
                    if (!unlocked)
                    {
                        // Wrong password — dialog will re-show
                    }
                }
            }

            // Initialize database
            var db = _serviceProvider.GetRequiredService<IDatabaseService>();
            db.Initialize();
            db.PruneExpired();

            // Run auto-cleanup if enabled
            if (settingsService.Settings.AutoCleanupEnabled)
            {
                var retentionDays = settingsService.Settings.RetentionDays;
                var excludePinned = settingsService.Settings.ExcludePinnedFromCleanup;
                db.PruneOlderThan(retentionDays, excludePinned);
            }

            // Create main VM
            var vm = _serviceProvider.GetRequiredService<MainWindowViewModel>();

            // Create main window
            _mainWindow = new MainWindow { DataContext = vm };
            desktop.MainWindow = _mainWindow;

            // Set explicit shutdown so closing hides to tray
            desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;

            // Start capture engine
            _capture = _serviceProvider.GetRequiredService<ICaptureService>();
            _capture.Start();

            // Start clipboard monitor
            _clipboardMonitor = _serviceProvider.GetRequiredService<IClipboardMonitorService>();
            _clipboardMonitor.Start();

            // Start screenshot capture
            _screenshotService = _serviceProvider.GetRequiredService<IScreenshotService>();
            _screenshotService.Start();

            // Start quick paste hotkey (Ctrl+Shift+V)
            if (settingsService.Settings.QuickPasteEnabled)
            {
                _hotkeyService = _serviceProvider.GetRequiredService<HotkeyService>();
                _hotkeyService.OnHotkeyPressed += () =>
                    Avalonia.Threading.Dispatcher.UIThread.Post(ShowQuickPaste);
                _hotkeyService.Start();
            }

            // Start cloud sync if configured
            if (settingsService.Settings.CloudSyncEnabled)
            {
                var syncManager = _serviceProvider.GetRequiredService<CloudSyncManager>();
                if (!string.IsNullOrEmpty(settingsService.Settings.CloudSyncProvider))
                    syncManager.SetActiveProvider(settingsService.Settings.CloudSyncProvider);
                syncManager.StartPeriodicSync(settingsService.Settings.CloudSyncIntervalMinutes);
            }

            // T-08: route every exit through one idempotent teardown. The tray Quit, the
            // encryption-cancel path, SettingsWindow's restart paths, and an OS shutdown all end
            // up calling desktop.Shutdown(), which fires ShutdownRequested -> OnShutdownRequested.
            _shutdown = new ShutdownCoordinator(
                _capture, _clipboardMonitor, _screenshotService, settingsService,
                ct => _serviceProvider!.GetRequiredService<CloudSyncManager>().SyncAsync(ct));
            desktop.ShutdownRequested += OnShutdownRequested;

            // Set up tray icon
            SetupTrayIcon(desktop, vm);

            // Show window on startup
            _mainWindow.Show();
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void ShowQuickPaste()
    {
        if (_quickPasteOpen) return;
        _quickPasteOpen = true;

        var quickPaste = new Views.QuickPasteWindow();

        quickPaste.Closed += async (_, _) =>
        {
            try
            {
                if (!string.IsNullOrEmpty(quickPaste.ContentToPaste))
                {
                    // Put the selected content on the clipboard
                    var clipboard = _mainWindow?.Clipboard;
                    if (clipboard != null)
                    {
                        await clipboard.SetTextAsync(quickPaste.ContentToPaste);

                        // Simulate Ctrl+V to paste into the previously active app
                        await Task.Delay(150);
                        SimulateCtrlV();
                    }
                }
            }
            finally
            {
                _quickPasteOpen = false;
            }
        };

        // Show as standalone window (not dialog) so it works even when main window is hidden
        quickPaste.Show();
    }

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, nuint dwExtraInfo);

    private static void SimulateCtrlV()
    {
        const byte VK_CONTROL = 0x11;
        const byte VK_V = 0x56;
        const uint KEYEVENTF_KEYUP = 0x0002;

        keybd_event(VK_CONTROL, 0, 0, 0);
        keybd_event(VK_V, 0, 0, 0);
        keybd_event(VK_V, 0, KEYEVENTF_KEYUP, 0);
        keybd_event(VK_CONTROL, 0, KEYEVENTF_KEYUP, 0);
    }

    public void ApplyTheme(string theme)
    {
        if (!Kapture.Themes.ThemeRegistry.Themes.TryGetValue(theme, out var def))
            def = Kapture.Themes.ThemeRegistry.Themes["Dark"];

        // Set FluentTheme base variant
        RequestedThemeVariant = def.BaseVariant == "Light" ? ThemeVariant.Light : ThemeVariant.Dark;

        // Programmatically replace all themed brush resources
        Resources["AppBgBrush"] = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse(def.BgPrimary));
        Resources["AppBgSecondary"] = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse(def.BgSecondary));
        Resources["AppBgTertiary"] = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse(def.BgTertiary));
        Resources["AppBorderBrush"] = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse(def.Border));
        Resources["AppTextPrimary"] = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse(def.TextPrimary));
        Resources["AppTextSecondary"] = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse(def.TextSecondary));
        Resources["AccentBrush"] = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse(def.Accent));
        Resources["AccentHoverBrush"] = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse(def.AccentHover));
        Resources["AccentDimBrush"] = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse(def.AccentDim));
    }

    private void SetupTrayIcon(IClassicDesktopStyleApplicationLifetime desktop, MainWindowViewModel vm)
    {
        var showItem = new NativeMenuItem("Open Vault");
        showItem.Click += (_, _) =>
        {
            _mainWindow?.Show();
            _mainWindow?.Activate();
            vm.Refresh();
        };

        var pauseItem = new NativeMenuItem("Pause Recording");
        pauseItem.Click += (_, _) =>
        {
            vm.ToggleRecordingCommand.Execute(null);
            pauseItem.Header = vm.IsRecording ? "Pause Recording" : "Resume Recording";
            UpdateTrayIcon(vm.IsRecording);
        };

        var settingsItem = new NativeMenuItem("Settings");
        settingsItem.Click += async (_, _) =>
        {
            if (_mainWindow == null) return;
            var settingsService = _serviceProvider!.GetRequiredService<ISettingsService>();
            var settingsVm = new SettingsViewModel(settingsService);
            var settingsWindow = new SettingsWindow { DataContext = settingsVm };
            await settingsWindow.ShowDialog(_mainWindow);
        };

        var separator = new NativeMenuItemSeparator();

        var quitItem = new NativeMenuItem("Quit");
        // All teardown (stop services, sync-on-close, dispose tray + ServiceProvider) runs once in
        // OnShutdownRequested — see T-08.
        quitItem.Click += (_, _) => desktop.Shutdown();

        _trayIcon = new TrayIcon
        {
            ToolTipText = "KaptureVault",
            Icon = new WindowIcon(AssetLoader.Open(new Uri("avares://KaptureVault/Assets/tray-recording.png"))),
            Menu = new NativeMenu { Items = { showItem, pauseItem, settingsItem, separator, quitItem } },
            IsVisible = true
        };

        _trayIcon.Clicked += (_, _) =>
        {
            _mainWindow?.Show();
            _mainWindow?.Activate();
            vm.Refresh();
        };
    }

    // T-08: the single teardown chokepoint. Every exit path calls Shutdown(), which fires this.
    // We cancel the first pass, run the (async, bounded) teardown, then re-issue Shutdown(); the
    // second pass sees _teardownDone and proceeds. Idempotent and safe from any exit path.
    private async void OnShutdownRequested(object? sender, ShutdownRequestedEventArgs e)
    {
        if (_teardownDone) return;
        e.Cancel = true;
        try
        {
            if (_shutdown is not null)
                await _shutdown.TeardownAsync(_syncOnShutdown, TimeSpan.FromSeconds(10));
        }
        catch { /* never let a teardown failure wedge shutdown */ }
        finally
        {
            _trayIcon?.Dispose();
            (_serviceProvider as IDisposable)?.Dispose(); // KV-024: also disposes HotkeyService + CloudSyncManager
            _teardownDone = true;
            (ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.Shutdown();
        }
    }

    /// <summary>
    /// Exit for SettingsWindow's elevate/de-elevate restart: tear down WITHOUT sync-on-close
    /// (a restart is not a quit) before the replacement process takes over.
    /// </summary>
    public void ShutdownForRestart()
    {
        _syncOnShutdown = false;
        (ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.Shutdown();
    }

    private void UpdateTrayIcon(bool isRecording)
    {
        if (_trayIcon == null) return;
        var iconName = isRecording ? "tray-recording.png" : "tray-paused.png";
        _trayIcon.Icon = new WindowIcon(AssetLoader.Open(new Uri($"avares://KaptureVault/Assets/{iconName}")));
        _trayIcon.ToolTipText = isRecording ? "KaptureVault - Recording" : "KaptureVault - Paused";
    }

    private void DisableAvaloniaDataAnnotationValidation()
    {
        var dataValidationPluginsToRemove =
            BindingPlugins.DataValidators.OfType<DataAnnotationsValidationPlugin>().ToArray();
        foreach (var plugin in dataValidationPluginsToRemove)
            BindingPlugins.DataValidators.Remove(plugin);
    }

}
