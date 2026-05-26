namespace Kapture.Services;

public record ActiveWindowInfo(string ProcessName, string WindowTitle);

public interface IActiveWindowService
{
    ActiveWindowInfo? GetActiveWindow();
}
