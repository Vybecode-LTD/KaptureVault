using FluentAssertions;
using Kapture.Models;
using Kapture.Services;
using Kapture.Services.CloudSync.Online;
using Kapture.ViewModels;
using NSubstitute;
using Xunit;

namespace KaptureVault.Tests.ViewModels;

/// <summary>
/// OnlineAccountViewModel (F-02 Phase 2): the Settings account panel's logic. Delegates to a mocked
/// IOnlineAccountService + IUrlOpener and persists the sync-provider choice via a mocked
/// ISettingsService — so sign-in/subscribe/billing/gating are verified without a browser or backend.
/// </summary>
public class OnlineAccountViewModelTests
{
    private readonly IOnlineAccountService _account = Substitute.For<IOnlineAccountService>();
    private readonly IUrlOpener _opener = Substitute.For<IUrlOpener>();
    private readonly ISettingsService _settings = Substitute.For<ISettingsService>();
    private readonly AppSettings _appSettings = new();

    private OnlineAccountViewModel NewVm(bool configured = true)
    {
        _settings.Settings.Returns(_appSettings);
        var config = configured
            ? new OnlineVaultConfig { ApiBaseUrl = "https://api.kapture.tools", GoogleClientId = "client-123" }
            : new OnlineVaultConfig { ApiBaseUrl = "REPLACE_WITH_API", GoogleClientId = "REPLACE_WITH_ID" }; // explicit not-configured (defaults are now real)
        return new OnlineAccountViewModel(_account, _opener, config, _settings);
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
    public async Task SignIn_WhenSucceedsAndPaid_PersistsOnlineVaultAsSyncProvider()
    {
        _account.SignInAsync(Arg.Any<CancellationToken>()).Returns(true);
        _account.IsPaid.Returns(true);
        var vm = NewVm();

        await vm.SignInCommand.ExecuteAsync(null);

        _appSettings.CloudSyncProvider.Should().Be("Online Vault");
        _appSettings.CloudSyncEnabled.Should().BeTrue();
        _settings.Received().Save();
    }

    [Fact]
    public async Task SignIn_WhenSucceedsButNotPaid_DoesNotSetProvider_AndPromptsToSubscribe()
    {
        _account.SignInAsync(Arg.Any<CancellationToken>()).Returns(true);
        _account.IsPaid.Returns(false);
        var vm = NewVm();

        await vm.SignInCommand.ExecuteAsync(null);

        _appSettings.CloudSyncProvider.Should().BeNull();
        vm.StatusMessage.Should().Contain("Subscribe");
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
    public void SignOut_ClearsProvider_WhenItWasOnlineVault()
    {
        _appSettings.CloudSyncProvider = "Online Vault";
        var vm = NewVm();

        vm.SignOutCommand.Execute(null);

        _account.Received(1).SignOut();
        _appSettings.CloudSyncProvider.Should().BeNull();
    }

    [Fact]
    public void AccountSummary_ShowsEmail_WhenSignedIn()
    {
        _account.IsSignedIn.Returns(true);
        _account.Email.Returns("a@b.com");

        NewVm().AccountSummary.Should().Be("Signed in as a@b.com");
    }
}
