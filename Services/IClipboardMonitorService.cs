namespace Kapture.Services;

public interface IClipboardMonitorService
{
    event Action? OnEntryFlushed;
    void Start();
    void Stop();
    void Pause();
    void Resume();
}
