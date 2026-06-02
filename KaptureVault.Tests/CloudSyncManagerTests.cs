using FluentAssertions;
using Kapture.Services;
using Kapture.Services.CloudSync;
using NSubstitute;
using Xunit;

namespace KaptureVault.Tests;

/// <summary>
/// P5 decouple: CloudSyncManager runs Google Drive backup and the Online Vault as two INDEPENDENT
/// features, each routed by ProviderName, each gated on its own provider's auth. These cover the
/// routing + no-op behaviour without touching the real vault: an unauthenticated provider short-circuits
/// before any file I/O, so SyncDriveNowAsync / SyncOnlineVaultNowAsync return false and never query the
/// provider. (The full LWW upload/download path needs the real %LOCALAPPDATA% vault + a live provider,
/// so it's exercised by the live smoke, not here — per the testing directive's "never touch the real vault".)
/// </summary>
public class CloudSyncManagerTests
{
    private static ICloudStorageProvider Provider(string name, bool authenticated = false)
    {
        var p = Substitute.For<ICloudStorageProvider>();
        p.ProviderName.Returns(name);
        p.IsAuthenticated.Returns(authenticated);
        return p;
    }

    private static CloudSyncManager Build(params ICloudStorageProvider[] providers) =>
        new(Substitute.For<IDatabaseService>(), providers);

    [Fact]
    public async Task SyncDriveNowAsync_WhenDriveNotConnected_ReturnsFalse_WithoutQuerying()
    {
        var drive = Provider(CloudSyncManager.DriveProviderName, authenticated: false);
        var mgr = Build(drive);

        var result = await mgr.SyncDriveNowAsync();

        result.Should().BeFalse();
        await drive.DidNotReceive().FindFileAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        mgr.IsSyncing.Should().BeFalse();
    }

    [Fact]
    public async Task SyncOnlineVaultNowAsync_WhenNotSignedIn_ReturnsFalse_WithoutQuerying()
    {
        var online = Provider(CloudSyncManager.OnlineVaultProviderName, authenticated: false);
        var mgr = Build(online);

        var result = await mgr.SyncOnlineVaultNowAsync();

        result.Should().BeFalse();
        await online.DidNotReceive().FindFileAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SyncDriveNowAsync_RoutesByName_AndIgnoresTheOnlineVaultProvider()
    {
        // Both registered; only the Online Vault is "connected". A Drive backup must NOT touch it.
        var drive = Provider(CloudSyncManager.DriveProviderName, authenticated: false);
        var online = Provider(CloudSyncManager.OnlineVaultProviderName, authenticated: true);
        var mgr = Build(drive, online);

        var result = await mgr.SyncDriveNowAsync();

        result.Should().BeFalse("Drive isn't connected");
        await online.DidNotReceive().FindFileAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SyncDriveNowAsync_WhenNoDriveProviderRegistered_ReturnsFalse()
    {
        var mgr = Build(Provider(CloudSyncManager.OnlineVaultProviderName));

        (await mgr.SyncDriveNowAsync()).Should().BeFalse();
    }

    [Fact]
    public async Task SyncOnlineVaultNowAsync_WhenNoOnlineProviderRegistered_ReturnsFalse()
    {
        var mgr = Build(Provider(CloudSyncManager.DriveProviderName));

        (await mgr.SyncOnlineVaultNowAsync()).Should().BeFalse();
    }

    [Fact]
    public async Task SyncAsync_WithUnauthenticatedProvider_ReturnsFalse_WithoutFileIO()
    {
        var p = Provider("anything", authenticated: false);
        var mgr = Build(p);

        (await mgr.SyncAsync(p)).Should().BeFalse();
        await p.DidNotReceive().FindFileAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SyncAsync_WithNullProvider_Throws()
    {
        var mgr = Build();

        var act = async () => await mgr.SyncAsync(null!);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public void Providers_ExposesRegisteredProvidersByName()
    {
        var mgr = Build(
            Provider(CloudSyncManager.DriveProviderName),
            Provider(CloudSyncManager.OnlineVaultProviderName));

        mgr.Providers.Keys.Should().BeEquivalentTo(
            [CloudSyncManager.DriveProviderName, CloudSyncManager.OnlineVaultProviderName]);
    }

    [Fact]
    public void StartingBothTimers_ThenDispose_DoesNotThrow()
    {
        var mgr = Build(
            Provider(CloudSyncManager.DriveProviderName),
            Provider(CloudSyncManager.OnlineVaultProviderName));

        var act = () =>
        {
            mgr.StartDriveBackup(15);
            mgr.StartOnlineVaultSync(15);
            mgr.StopDriveBackup();
            mgr.StopOnlineVaultSync();
            mgr.Dispose();
        };

        act.Should().NotThrow();
    }
}
