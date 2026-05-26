namespace Kapture.Services;

public interface IScreenshotService
{
    event Action? OnEntryFlushed;
    void Start();
    void Stop();
    void Pause();
    void Resume();
}
