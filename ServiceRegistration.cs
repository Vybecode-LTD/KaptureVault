using Kapture.Services;
using Kapture.Services.CloudSync;
using Kapture.Services.CloudSync.Online;
using Kapture.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace Kapture;

/// <summary>
/// Composition root for KaptureVault's DI container. Extracted from <see cref="App"/>
/// (KV-010 / T-10) so the exact service graph can be registered in tests, and so the
/// hotkey service and view models come from the container instead of being <c>new</c>'d
/// in <c>App</c> or located via <c>App.Services</c> in views.
/// </summary>
public static class ServiceRegistration
{
    /// <summary>Registers every KaptureVault service, the hotkey service, and the view models.</summary>
    public static IServiceCollection AddKaptureServices(this IServiceCollection services)
    {
        // Core services (singletons). Encryption is registered before the database so the
        // database factory can pull it from the provider.
        services.AddSingleton<ISettingsService, SettingsService>();
        services.AddSingleton<IEncryptionService, EncryptionService>();
        services.AddSingleton<IDatabaseService>(sp => new DatabaseService(sp.GetRequiredService<IEncryptionService>()));
        services.AddSingleton<IActiveWindowService, ActiveWindowService>();
        services.AddSingleton<ICaptureService, CaptureService>();
        services.AddSingleton<IKeyboardHookService, KeyboardHookService>();
        services.AddSingleton<IClipboardMonitorService, ClipboardMonitorService>();
        services.AddSingleton<IScreenshotService, ScreenshotService>();
        services.AddSingleton<IStartupService, StartupService>();

        // F-02 Online Vault (paid tier): the account/session layer + its R2 sync provider.
        // OnlineVaultConfig carries the (deploy-time) Worker base URL + public sign-in client id;
        // no Google/storage secret ever lives client-side. HttpClients are simple singletons here,
        // matching GoogleDriveProvider's existing pattern.
        services.AddSingleton<OnlineVaultConfig>();
        services.AddSingleton<IGoogleSignIn, LoopbackGoogleSignIn>();
        services.AddSingleton<IOnlineTokenStore, DpapiOnlineTokenStore>();
        services.AddSingleton<IKaptureOnlineApiClient>(sp =>
            new KaptureOnlineApiClient(new HttpClient(), sp.GetRequiredService<OnlineVaultConfig>().ApiBaseUrl));
        services.AddSingleton<IOnlineAccountService, OnlineAccountService>();
        services.AddSingleton<IUrlOpener, BrowserUrlOpener>();

        // The two INDEPENDENT cloud features (P5 decouple), each registered as an ICloudStorageProvider
        // and looked up by ProviderName inside CloudSyncManager: Google Drive backup + the Online Vault.
        services.AddSingleton<ICloudStorageProvider, GoogleDriveProvider>();
        services.AddSingleton<ICloudStorageProvider>(sp => new R2StorageProvider(
            sp.GetRequiredService<IOnlineAccountService>(),
            sp.GetRequiredService<IKaptureOnlineApiClient>(),
            new HttpClient(),
            sp.GetRequiredService<IEncryptionService>()));

        // Phase 3 (slice F): the Online Vault screenshot pipeline — re-encode (BMP→PNG), encrypt, and
        // upload the screenshots the capture DB references, alongside vault.db. CloudSyncManager invokes
        // it after an Online-Vault upload. Its own HttpClient for the R2 object transfers (mirrors R2StorageProvider).
        services.AddSingleton<IScreenshotImageCodec, SkiaScreenshotImageCodec>();
        services.AddSingleton<IScreenshotSyncService>(sp => new ScreenshotSyncService(
            sp.GetRequiredService<IOnlineAccountService>(),
            sp.GetRequiredService<IKaptureOnlineApiClient>(),
            new HttpClient(),
            sp.GetRequiredService<IEncryptionService>(),
            sp.GetRequiredService<IScreenshotImageCodec>(),
            sp.GetRequiredService<IDatabaseService>()));
        services.AddSingleton<CloudSyncManager>();
        // The on-demand sync trigger the Online Vault UI binds to (same singleton).
        services.AddSingleton<IOnlineVaultSync>(sp => sp.GetRequiredService<CloudSyncManager>());

        // KV-010 / T-10: previously `new HotkeyService()` / `new MainWindowViewModel(...)`
        // in App. HotkeyService is parameterless; MainWindowViewModel uses an explicit
        // factory that mirrors the original construction exactly, so resolution can't pick
        // a wrong/ambiguous constructor (the view model also has a design-time ctor).
        services.AddSingleton<HotkeyService>();
        services.AddSingleton<OnlineAccountViewModel>();
        services.AddSingleton<MainWindowViewModel>(sp => new MainWindowViewModel(
            sp.GetRequiredService<IDatabaseService>(),
            sp.GetRequiredService<ICaptureService>(),
            sp.GetRequiredService<IClipboardMonitorService>(),
            sp.GetRequiredService<IStartupService>(),
            sp.GetRequiredService<ISettingsService>(),
            sp.GetRequiredService<IScreenshotService>(),
            sp.GetRequiredService<OnlineAccountViewModel>()));

        return services;
    }
}
