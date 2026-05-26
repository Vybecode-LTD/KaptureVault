using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Kapture.Services;

namespace Kapture.ViewModels;

public partial class SettingsViewModel : ViewModelBase
{
    private readonly ISettingsService _settings;

    // Theme
    [ObservableProperty] private string _selectedTheme = "Dark";

    // Auto-Cleanup
    [ObservableProperty] private bool _autoCleanupEnabled = true;
    [ObservableProperty] private int _retentionDays = 30;
    [ObservableProperty] private bool _excludePinnedFromCleanup = true;

    // Quick Paste
    [ObservableProperty] private bool _quickPasteEnabled = true;
    [ObservableProperty] private string _quickPasteHotkey = "Ctrl+Shift+V";

    // Buffer
    [ObservableProperty] private int _maxBufferChars = 5000;
    [ObservableProperty] private int _idleFlushSeconds = 20;

    // Cloud Sync
    [ObservableProperty] private bool _cloudSyncEnabled = false;
    [ObservableProperty] private int _cloudSyncIntervalMinutes = 15;
    [ObservableProperty] private bool _syncOnClose = true;

    // Status
    [ObservableProperty] private bool _hasChanges;

    public SettingsViewModel()
    {
        // Design-time only
        _settings = null!;
    }

    public SettingsViewModel(ISettingsService settings)
    {
        _settings = settings;
        LoadFromSettings();
    }

    private void LoadFromSettings()
    {
        var s = _settings.Settings;
        SelectedTheme = s.Theme;
        AutoCleanupEnabled = s.AutoCleanupEnabled;
        RetentionDays = s.RetentionDays;
        ExcludePinnedFromCleanup = s.ExcludePinnedFromCleanup;
        QuickPasteEnabled = s.QuickPasteEnabled;
        QuickPasteHotkey = s.QuickPasteHotkey;
        MaxBufferChars = s.MaxBufferChars;
        IdleFlushSeconds = s.IdleFlushSeconds;
        CloudSyncEnabled = s.CloudSyncEnabled;
        CloudSyncIntervalMinutes = s.CloudSyncIntervalMinutes;
        SyncOnClose = s.SyncOnClose;
        HasChanges = false;
    }

    // Track changes on any property update
    partial void OnSelectedThemeChanged(string value) => HasChanges = true;
    partial void OnAutoCleanupEnabledChanged(bool value) => HasChanges = true;
    partial void OnRetentionDaysChanged(int value) => HasChanges = true;
    partial void OnExcludePinnedFromCleanupChanged(bool value) => HasChanges = true;
    partial void OnQuickPasteEnabledChanged(bool value) => HasChanges = true;
    partial void OnQuickPasteHotkeyChanged(string value) => HasChanges = true;
    partial void OnMaxBufferCharsChanged(int value) => HasChanges = true;
    partial void OnIdleFlushSecondsChanged(int value) => HasChanges = true;
    partial void OnCloudSyncEnabledChanged(bool value) => HasChanges = true;
    partial void OnCloudSyncIntervalMinutesChanged(int value) => HasChanges = true;
    partial void OnSyncOnCloseChanged(bool value) => HasChanges = true;

    [RelayCommand]
    private void SaveSettings()
    {
        var s = _settings.Settings;
        s.Theme = SelectedTheme;
        s.AutoCleanupEnabled = AutoCleanupEnabled;
        s.RetentionDays = RetentionDays;
        s.ExcludePinnedFromCleanup = ExcludePinnedFromCleanup;
        s.QuickPasteEnabled = QuickPasteEnabled;
        s.QuickPasteHotkey = QuickPasteHotkey;
        s.MaxBufferChars = MaxBufferChars;
        s.IdleFlushSeconds = IdleFlushSeconds;
        s.CloudSyncEnabled = CloudSyncEnabled;
        s.CloudSyncIntervalMinutes = CloudSyncIntervalMinutes;
        s.SyncOnClose = SyncOnClose;
        _settings.Save();
        HasChanges = false;
    }

    [RelayCommand]
    private void ResetDefaults()
    {
        var defaults = new Models.AppSettings();
        SelectedTheme = defaults.Theme;
        AutoCleanupEnabled = defaults.AutoCleanupEnabled;
        RetentionDays = defaults.RetentionDays;
        ExcludePinnedFromCleanup = defaults.ExcludePinnedFromCleanup;
        QuickPasteEnabled = defaults.QuickPasteEnabled;
        QuickPasteHotkey = defaults.QuickPasteHotkey;
        MaxBufferChars = defaults.MaxBufferChars;
        IdleFlushSeconds = defaults.IdleFlushSeconds;
        CloudSyncEnabled = defaults.CloudSyncEnabled;
        CloudSyncIntervalMinutes = defaults.CloudSyncIntervalMinutes;
        SyncOnClose = defaults.SyncOnClose;
    }
}
