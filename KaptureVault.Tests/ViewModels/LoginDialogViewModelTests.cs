using FluentAssertions;
using Kapture.Services;
using Kapture.Services.CloudSync.Online;
using Kapture.ViewModels;
using NSubstitute;
using Xunit;

namespace KaptureVault.Tests.ViewModels;

/// <summary>
/// The Phase 5 login dialog VM: email/password sign-in, register (with the §42 account-vs-vault
/// password interlock), reset-request, and Continue-with-Google. The account service + encryption
/// service are mocked.
/// </summary>
public class LoginDialogViewModelTests
{
    private readonly IOnlineAccountService _account = Substitute.For<IOnlineAccountService>();
    private readonly IEncryptionService _encryption = Substitute.For<IEncryptionService>();

    private LoginDialogViewModel NewVm() => new(_account, _encryption);

    [Fact]
    public async Task Register_RefusesAccountPasswordEqualToVaultPassword()
    {
        // A vault password is configured, and the chosen account password equals it -> interlock.
        _encryption.IsConfigured.Returns(true);
        _encryption.VerifyPassword("vault-secret").Returns(true);

        var vm = NewVm();
        vm.Mode = LoginMode.Register;
        vm.Email = "a@b.com";
        vm.Password = "vault-secret";
        vm.ConfirmPassword = "vault-secret";

        await vm.SubmitCommand.ExecuteAsync(null);

        vm.StatusMessage.Should().Contain("different from your vault");
        await _account.DidNotReceive().RegisterAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Register_RefusesMismatchedConfirmation()
    {
        var vm = NewVm();
        vm.Mode = LoginMode.Register;
        vm.Email = "a@b.com";
        vm.Password = "password1";
        vm.ConfirmPassword = "password2";

        await vm.SubmitCommand.ExecuteAsync(null);

        vm.StatusMessage.Should().Contain("don't match");
        await _account.DidNotReceive().RegisterAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Register_RefusesShortPassword()
    {
        var vm = NewVm();
        vm.Mode = LoginMode.Register;
        vm.Email = "a@b.com";
        vm.Password = "short";
        vm.ConfirmPassword = "short";

        await vm.SubmitCommand.ExecuteAsync(null);

        vm.StatusMessage.Should().Contain("at least 8");
        await _account.DidNotReceive().RegisterAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Register_Valid_CallsRegister_AndSwitchesToSignIn()
    {
        _encryption.IsConfigured.Returns(false); // no vault password -> interlock not applicable
        _account.RegisterAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(true);

        var vm = NewVm();
        vm.Mode = LoginMode.Register;
        vm.Email = "new@b.com";
        vm.Password = "password1";
        vm.ConfirmPassword = "password1";

        await vm.SubmitCommand.ExecuteAsync(null);

        await _account.Received(1).RegisterAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        vm.StatusMessage.Should().Contain("Check your email");
        vm.Mode.Should().Be(LoginMode.SignIn);
    }

    [Fact]
    public async Task SignIn_Success_RaisesSignedIn()
    {
        _account.SignInWithPasswordAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(true);

        var vm = NewVm();
        var signedIn = false;
        vm.SignedIn += () => signedIn = true;
        vm.Mode = LoginMode.SignIn;
        vm.Email = "a@b.com";
        vm.Password = "password1";

        await vm.SubmitCommand.ExecuteAsync(null);

        signedIn.Should().BeTrue();
    }

    [Fact]
    public async Task SignIn_Unverified_ShowsVerifyMessage_NoSignedIn()
    {
        _account.SignInWithPasswordAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(false);
        _account.NeedsVerification.Returns(true);

        var vm = NewVm();
        var signedIn = false;
        vm.SignedIn += () => signedIn = true;
        vm.Mode = LoginMode.SignIn;
        vm.Email = "unv@b.com";
        vm.Password = "password1";

        await vm.SubmitCommand.ExecuteAsync(null);

        signedIn.Should().BeFalse();
        vm.StatusMessage.Should().Contain("verify your email");
    }

    [Fact]
    public async Task Forgot_AlwaysShowsNeutralMessage()
    {
        _account.RequestPasswordResetAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(true);

        var vm = NewVm();
        vm.Mode = LoginMode.Forgot;
        vm.Email = "a@b.com";

        await vm.SubmitCommand.ExecuteAsync(null);

        vm.StatusMessage.Should().Contain("If that email has an account");
        vm.Mode.Should().Be(LoginMode.SignIn);
    }

    [Fact]
    public async Task ContinueWithGoogle_Success_RaisesSignedIn()
    {
        _account.SignInAsync(Arg.Any<CancellationToken>()).Returns(true);

        var vm = NewVm();
        var signedIn = false;
        vm.SignedIn += () => signedIn = true;

        await vm.ContinueWithGoogleCommand.ExecuteAsync(null);

        signedIn.Should().BeTrue();
    }
}
