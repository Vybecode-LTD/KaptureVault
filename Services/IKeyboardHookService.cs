namespace Kapture.Services;

public interface IKeyboardHookService
{
    event Action<char>? OnCharTyped;
    event Action? OnBackspace;
    event Action? OnEnter;
    event Action? OnTab;
    void Start();
    void Stop();
}
