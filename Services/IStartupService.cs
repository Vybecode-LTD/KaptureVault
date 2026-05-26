namespace Kapture.Services;

public interface IStartupService
{
    bool IsRegistered { get; }
    void Register();
    void Unregister();
}
