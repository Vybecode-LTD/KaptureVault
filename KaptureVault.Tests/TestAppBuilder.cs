using Avalonia;
using Avalonia.Headless;
using KaptureVault.Tests;

[assembly: AvaloniaTestApplication(typeof(TestAppBuilder))]

namespace KaptureVault.Tests;

/// <summary>
/// T-16: headless test application for <c>[AvaloniaFact]</c> / <c>[AvaloniaTheory]</c>.
/// Reuses the real <see cref="Kapture.App"/> so App.axaml resources/themes load, but the headless
/// session never sets a classic desktop lifetime, so App.OnFrameworkInitializationCompleted's heavy
/// startup (DI, keyboard hook, tray, sync) is skipped — that block is gated on
/// <c>ApplicationLifetime is IClassicDesktopStyleApplicationLifetime</c>.
/// </summary>
public static class TestAppBuilder
{
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<Kapture.App>()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions());
}
