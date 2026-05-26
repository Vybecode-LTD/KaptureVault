using Kapture.Models;

namespace Kapture.Services;

public interface ISettingsService
{
    AppSettings Settings { get; }
    void Save();
    void Load();
    event Action? OnSettingsChanged;
}
