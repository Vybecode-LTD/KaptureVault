using FluentAssertions;
using Kapture;
using Kapture.Services;
using Kapture.Services.CloudSync;
using Kapture.Services.CloudSync.Online;
using Kapture.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace KaptureVault.Tests;

/// <summary>
/// KV-010 / T-10: the composition root (<see cref="ServiceRegistration.AddKaptureServices"/>)
/// must register every service plus the hotkey service and the main view model — the latter
/// two were previously <c>new</c>'d in App / located via <c>App.Services</c> in views.
///
/// These assert registration only (not construction): building the real graph would create
/// the production <see cref="DatabaseService"/> (touching %LOCALAPPDATA%) and a view model
/// that uses the Avalonia dispatcher. Full resolution belongs to the headless harness (T-16).
/// </summary>
public class ServiceRegistrationTests
{
    [Theory]
    [InlineData(typeof(ISettingsService))]
    [InlineData(typeof(IEncryptionService))]
    [InlineData(typeof(IDatabaseService))]
    [InlineData(typeof(IActiveWindowService))]
    [InlineData(typeof(ICaptureService))]
    [InlineData(typeof(IKeyboardHookService))]
    [InlineData(typeof(IClipboardMonitorService))]
    [InlineData(typeof(IScreenshotService))]
    [InlineData(typeof(IStartupService))]
    [InlineData(typeof(CloudSyncManager))]
    [InlineData(typeof(HotkeyService))]
    [InlineData(typeof(MainWindowViewModel))]
    [InlineData(typeof(OnlineVaultConfig))]
    [InlineData(typeof(IGoogleSignIn))]
    [InlineData(typeof(IOnlineTokenStore))]
    [InlineData(typeof(IKaptureOnlineApiClient))]
    [InlineData(typeof(IOnlineAccountService))]
    [InlineData(typeof(IUrlOpener))]
    [InlineData(typeof(OnlineAccountViewModel))]
    [InlineData(typeof(IScreenshotImageCodec))]
    [InlineData(typeof(IScreenshotSyncService))]
    [InlineData(typeof(ISyncProviderController))]
    public void AddKaptureServices_RegistersExpectedType(Type serviceType)
    {
        var services = new ServiceCollection().AddKaptureServices();

        services.Should().Contain(d => d.ServiceType == serviceType,
            $"{serviceType.Name} must be registered in the composition root");
    }

    [Fact]
    public void AddKaptureServices_RegistersHotkeyAndViewModel_PreviouslyNewedInApp()
    {
        // The specific regression T-10 guards: these two used to bypass DI.
        var services = new ServiceCollection().AddKaptureServices();

        services.Should().Contain(d => d.ServiceType == typeof(HotkeyService));
        services.Should().Contain(d => d.ServiceType == typeof(MainWindowViewModel));
    }

    [Fact]
    public void AddKaptureServices_RegistersBothCloudProviders()
    {
        // CloudSyncManager consumes IEnumerable<ICloudStorageProvider>; both the free (Drive) and
        // paid (Online Vault) providers must be present so either can be selected by name.
        var services = new ServiceCollection().AddKaptureServices();

        services.Where(d => d.ServiceType == typeof(ICloudStorageProvider))
            .Should().HaveCount(2, "Google Drive (free) and Online Vault (paid) are both selectable");
    }
}
