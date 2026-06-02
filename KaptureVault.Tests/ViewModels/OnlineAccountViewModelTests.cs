using FluentAssertions;
using Kapture.Services;
using Kapture.Services.CloudSync.Online;
using Kapture.ViewModels;
using NSubstitute;
using Xunit;

namespace KaptureVault.Tests.ViewModels;

/// <summary>
/// OnlineAccountViewModel (F-02): the Settings account panel's logic. Delegates to a mocked
/// IOnlineAccountService + IUrlOpener + IEncryptionService — so sign-in/subscribe/billing/gating are
/// verified without a browser or backend. P5 decouple: the panel is account-state only; it no longer
/// picks a sync "provider" or writes settings (the Online Vault syncs automatically once signed in
/// with a vault password — see CloudSyncManagerTests).
/// </summary>
public class OnlineAccountViewModelTests
{
    private readonly IOnlineAccountService _account = Substitute.For<IOnlineAccountService>();
    private readonly IUrlOpener _opener = Substitute.For<IUrlOpener>();
    private readonly IEncryptionService _enc = Substitute.For<IEncryptionService>();

    private OnlineAccountViewModel NewVm(bool configured = true)
    {
        var config = configured
            ? new OnlineVaultConfig { ApiBaseUrl = "https://api.kapture.tools", GoogleClientId = "client-123" }
            : new OnlineVaultConfig { ApiBaseUrl = "REPLACE_WITH_API", GoogleClientId = "REPLACE_WITH_ID" }; // explicit not-configured (defaults are now real)
        return new OnlineAccountViewModel(_account, _opener, config, _enc);
    }

    [Fact]
    public void IsConfigured_ReflectsConfig()
    {
        NewVm(configured: true).IsConfigured.Should().BeTrue();
        NewVm(configured: false).IsConfigured.Should().BeFalse();
    }

    [Fact]
    public async Task SignIn_WhenNotConfigured_ShowsMessage_AndDoesNotCallAccount()
    {
        var vm = NewVm(configured: false);

        await vm.SignInCommand.ExecuteAsync(null);

        await _account.DidNotReceive().SignInAsync(Arg.Any<CancellationToken>());
        vm.StatusMessage.Should().Contain("isn't configured");
    }

    [Fact]
    public async Task SignIn_WhenVaultEncrypted_ReportsAutomaticSync()
    {
        // Vault sync is free (Phase 2) and the Online Vault is now automatic once signed in with an
        // active vault password (Phase 3 slice B + P5 decouple) — no subscription, no provider to pick.
        _account.SignInAsync(Arg.Any<CancellationToken>()).Returns(true);
        _account.IsSignedIn.Returns(true); // a real sign-in flips this
        _enc.IsActive.Returns(true);
        var vm = NewVm();

        await vm.SignInCommand.ExecuteAsync(null);

        await _account.Received(1).RefreshAccountAsync(Arg.Any<CancellationToken>());
        vm.StatusMessage.Should().Contain("automatically");
        vm.IsSyncingAutomatically.Should().BeTrue();
    }

    [Fact]
    public async Task SignIn_WhenNoVaultPassword_PromptsToSetOne()
    {
        _account.SignInAsync(Arg.Any<CancellationToken>()).Returns(true);
        _account.IsSignedIn.Returns(true); // a real sign-in flips this
        _enc.IsActive.Returns(false); // no vault password → the (E2E-encrypted) Online Vault can't sync yet
        var vm = NewVm();

        await vm.SignInCommand.ExecuteAsync(null);

        vm.StatusMessage.Should().Contain("vault password");
        vm.IsSyncingAutomatically.Should().BeFalse();
        vm.VaultPasswordRequired.Should().BeTrue();
    }

    [Fact]
    public async Task SignIn_WhenCancelled_ShowsLastError()
    {
        _account.SignInAsync(Arg.Any<CancellationToken>()).Returns(false);
        _account.LastError.Returns("Sign-in was cancelled.");
        var vm = NewVm();

        await vm.SignInCommand.ExecuteAsync(null);

        vm.StatusMessage.Should().Be("Sign-in was cancelled.");
    }

    [Fact]
    public async Task Subscribe_OpensCheckoutUrl()
    {
        _account.GetCheckoutUrlAsync(Arg.Any<CancellationToken>()).Returns("https://stripe/checkout");
        var vm = NewVm();

        await vm.SubscribeCommand.ExecuteAsync(null);

        _opener.Received(1).Open("https://stripe/checkout");
    }

    [Fact]
    public async Task Subscribe_WhenNoUrl_ShowsError_AndDoesNotOpen()
    {
        _account.GetCheckoutUrlAsync(Arg.Any<CancellationToken>()).Returns((string?)null);
        _account.LastError.Returns("Couldn't start checkout.");
        var vm = NewVm();

        await vm.SubscribeCommand.ExecuteAsync(null);

        _opener.DidNotReceive().Open(Arg.Any<string>());
        vm.StatusMessage.Should().Contain("Couldn't start checkout");
    }

    [Fact]
    public async Task ManageBilling_OpensPortalUrl()
    {
        _account.GetBillingPortalUrlAsync(Arg.Any<CancellationToken>()).Returns("https://stripe/portal");
        var vm = NewVm();

        await vm.ManageBillingCommand.ExecuteAsync(null);

        _opener.Received(1).Open("https://stripe/portal");
    }

    [Fact]
    public void SignOut_SignsOut()
    {
        var vm = NewVm();

        vm.SignOutCommand.Execute(null);

        _account.Received(1).SignOut();
        vm.StatusMessage.Should().Be("Signed out.");
    }

    [Fact]
    public void AccountSummary_ShowsEmail_WhenSignedIn()
    {
        _account.IsSignedIn.Returns(true);
        _account.Email.Returns("a@b.com");

        NewVm().AccountSummary.Should().Be("Signed in as a@b.com");
    }

    [Fact]
    public void StorageSummary_FormatsUsedOfQuota_WhenSignedInWithQuota()
    {
        _account.IsSignedIn.Returns(true);
        _account.QuotaBytes.Returns(250L * 1024 * 1024);
        _account.UsedBytes.Returns(5L * 1024 * 1024);

        var vm = NewVm();

        vm.HasStorageInfo.Should().BeTrue();
        vm.StorageSummary.Should().Be("5 MB of 250 MB used");
    }

    [Fact]
    public void StorageSummary_IsEmpty_WhenNoQuotaKnown()
    {
        _account.IsSignedIn.Returns(true);
        _account.QuotaBytes.Returns(0L);

        var vm = NewVm();

        vm.HasStorageInfo.Should().BeFalse();
        vm.StorageSummary.Should().BeEmpty();
    }

    [Fact]
    public void VaultPasswordRequired_TrueOnlyWhenSignedInWithoutActiveEncryption()
    {
        _account.IsSignedIn.Returns(true);
        _enc.IsActive.Returns(false);
        NewVm().VaultPasswordRequired.Should().BeTrue();

        _enc.IsActive.Returns(true);
        NewVm().VaultPasswordRequired.Should().BeFalse();
    }

    [Fact]
    public void IsSyncingAutomatically_TrueOnlyWhenSignedInWithActiveEncryption()
    {
        _account.IsSignedIn.Returns(false);
        _enc.IsActive.Returns(true);
        NewVm().IsSyncingAutomatically.Should().BeFalse("not signed in");

        _account.IsSignedIn.Returns(true);
        _enc.IsActive.Returns(false);
        NewVm().IsSyncingAutomatically.Should().BeFalse("no vault password");

        _account.IsSignedIn.Returns(true);
        _enc.IsActive.Returns(true);
        NewVm().IsSyncingAutomatically.Should().BeTrue("signed in + vault password");
    }

    [Fact]
    public void OpenVault_OpensTheWebVaultUrl()
    {
        NewVm().OpenVaultCommand.Execute(null);

        _opener.Received(1).Open(Arg.Is<string>(u => u.Contains("kapture.tools/vault")));
    }
}
