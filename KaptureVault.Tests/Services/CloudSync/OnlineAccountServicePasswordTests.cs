using System.Net;
using FluentAssertions;
using Kapture.Services.CloudSync.Online;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;

namespace KaptureVault.Tests.Services.CloudSync;

/// <summary>
/// Phase 5: the email/password account methods on <see cref="OnlineAccountService"/> — register,
/// verify, login, reset-request, reset. The backend API is mocked; a fixed clock makes the stored
/// session's expiry deterministic. (The account-vs-vault-password interlock is enforced one layer
/// up, in the ViewModel — see the OnlineAccountViewModel tests.)
/// </summary>
public class OnlineAccountServicePasswordTests
{
    private static OnlineSession Session(string s = "sess", string r = "refresh", string uid = "u-1", int exp = 3600)
        => new(s, r, uid, exp);

    private readonly IKaptureOnlineApiClient _api = Substitute.For<IKaptureOnlineApiClient>();
    private readonly IGoogleSignIn _signIn = Substitute.For<IGoogleSignIn>();
    private readonly InMemoryTokenStore _store = new();
    private readonly DateTime _now = new(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc);

    private OnlineAccountService NewService() => new(_api, _signIn, _store, () => _now);

    [Fact]
    public async Task Register_ReturnsTrue_AndDoesNotSignIn()
    {
        _api.RegisterAsync("new@x.com", "password1", Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        var svc = NewService();

        (await svc.RegisterAsync("new@x.com", "password1")).Should().BeTrue();
        svc.IsSignedIn.Should().BeFalse(); // registration emails a verify link; no session yet
    }

    [Fact]
    public async Task Register_WhenAlreadyExists_ReturnsFalse_WithError()
    {
        _api.RegisterAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new OnlineApiException(HttpStatusCode.Conflict, "an account with this email already exists"));
        var svc = NewService();

        (await svc.RegisterAsync("dup@x.com", "password1")).Should().BeFalse();
        svc.LastError.Should().Contain("already exists");
    }

    [Fact]
    public async Task SignInWithPassword_StoresSession()
    {
        _api.LoginAsync("a@b.com", "password1", Arg.Any<CancellationToken>()).Returns(Session(uid: "u-9"));
        var svc = NewService();

        (await svc.SignInWithPasswordAsync("a@b.com", "password1")).Should().BeTrue();
        svc.IsSignedIn.Should().BeTrue();
        svc.Uid.Should().Be("u-9");
        _store.Tokens!.Session.Should().Be("sess");
    }

    [Fact]
    public async Task SignInWithPassword_On403_SetsNeedsVerification()
    {
        _api.LoginAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new OnlineApiException(HttpStatusCode.Forbidden, "please verify your email first"));
        var svc = NewService();

        (await svc.SignInWithPasswordAsync("unv@x.com", "password1")).Should().BeFalse();
        svc.IsSignedIn.Should().BeFalse();
        svc.NeedsVerification.Should().BeTrue();
    }

    [Fact]
    public async Task SignInWithPassword_On401_DoesNotSetNeedsVerification()
    {
        _api.LoginAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new OnlineApiException(HttpStatusCode.Unauthorized, "invalid email or password"));
        var svc = NewService();

        (await svc.SignInWithPasswordAsync("a@b.com", "wrong")).Should().BeFalse();
        svc.NeedsVerification.Should().BeFalse();
    }

    [Fact]
    public async Task VerifyEmail_StoresSession()
    {
        _api.VerifyEmailAsync("tok", Arg.Any<CancellationToken>()).Returns(Session(uid: "u-2"));
        var svc = NewService();

        (await svc.VerifyEmailAsync("tok")).Should().BeTrue();
        svc.IsSignedIn.Should().BeTrue();
        svc.Uid.Should().Be("u-2");
    }

    [Fact]
    public async Task RequestPasswordReset_ReturnsTrue()
    {
        _api.RequestPasswordResetAsync("a@b.com", Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        var svc = NewService();

        (await svc.RequestPasswordResetAsync("a@b.com")).Should().BeTrue();
    }

    [Fact]
    public async Task ResetPassword_StoresSession()
    {
        _api.ResetPasswordAsync("tok", "newpassword", Arg.Any<CancellationToken>()).Returns(Session(uid: "u-3"));
        var svc = NewService();

        (await svc.ResetPasswordAsync("tok", "newpassword")).Should().BeTrue();
        svc.IsSignedIn.Should().BeTrue();
        svc.Uid.Should().Be("u-3");
    }

    private sealed class InMemoryTokenStore : IOnlineTokenStore
    {
        public OnlineTokens? Tokens;
        public OnlineTokens? Load() => Tokens;
        public void Save(OnlineTokens tokens) => Tokens = tokens;
        public void Clear() => Tokens = null;
    }
}
