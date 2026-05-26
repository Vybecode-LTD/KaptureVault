namespace Kapture.Services;

public interface ICaptureService
{
    bool IsRecording { get; }
    event Action? OnEntryFlushed;
    void Start();
    void Stop();
    void Pause();
    void Resume();
}
