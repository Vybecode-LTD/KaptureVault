using System.Text.Json;
using Kapture.Models;
using Microsoft.Extensions.Logging;

namespace Kapture.Services;

public class SettingsService : ISettingsService
{
    private static readonly string SettingsDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".kapture");

    private static readonly string SettingsPath = Path.Combine(SettingsDir, "settings.json");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private readonly ILogger<SettingsService>? _log;

    public AppSettings Settings { get; private set; } = new();
    public event Action? OnSettingsChanged;

    public SettingsService(ILogger<SettingsService>? log = null)
    {
        _log = log;
    }

    public void Load()
    {
        try
        {
            if (File.Exists(SettingsPath))
            {
                var json = File.ReadAllText(SettingsPath);
                Settings = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions) ?? new AppSettings();
            }
        }
        catch (Exception ex)
        {
            _log?.LogWarning(ex, "Failed to load settings from {Path} — using defaults", SettingsPath);
            Settings = new AppSettings();
        }
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(SettingsDir);
            var json = JsonSerializer.Serialize(Settings, JsonOptions);
            File.WriteAllText(SettingsPath, json);
            OnSettingsChanged?.Invoke();
        }
        catch (Exception ex)
        {
            _log?.LogError(ex, "Failed to save settings to {Path}", SettingsPath);
        }
    }
}
