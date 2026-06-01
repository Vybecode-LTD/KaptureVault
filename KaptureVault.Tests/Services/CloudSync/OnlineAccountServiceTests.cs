using System.Net;
using FluentAssertions;
using Kapture.Services.CloudSync.Online;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;

namespace KaptureVault.Tests.Services.CloudSync;

/// <summary>
/// The Online Vault account/session layer (F-02 Phase 2): secret-less sign-in via the broker,
/// DPAPI-persisted session with transparent refresh (near-expiry + 401 retry), sign-out, and the
/// cached subscription entitlement that gates paid UI. The browser/loopback and DPAPI edges are
/// mocked (IGoogleSignIn + an in-memory token store); a fixed clock makes expiry deterministic.
/// </summary>
public class OnlineAccountServiceTests
{
    private static GoogleAuthCode Code => new("code-1", "verifier-1", "http://localhost:48722/");

    private static OnlineSession Session(string s = "sess-1", string r = "refresh-1", string uid = "u-1", int exp = 3600)
        => new(s, r, uid, exp);

    private readonly IKaptureOnlineApiClient _api = Substitute.For<IKaptureOnlineApiClient>();
    private readonly IGoogleSignIn _signIn = Substitute.For<IGoogleSignIn>();
    private readonly InMemoryTokenStore _store = new();
    private readonly DateTime _now = new(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc);

    private OnlineAccountService NewService() => new(_api, _signIn, _store, () => _now);

    [Fact]
    public async Task SignInAsync_StoresSessionAndSetsUid()
    {
        _signIn.SignInAsync(Arg.Any<CancellationToken>()).Returns(Code);
        _api.AuthWithCodeAsync("code-1", "verifier-1", "http://localhost:48722/", Arg.Any<CancellationToken>())
            .Returns(Session(uid: "u-42"));

        var svc = NewService();
        var ok = await svc.SignInAsync();

        ok.Should().BeTrue();
        svc.IsSignedIn.Should().BeTrue();
        svc.Uid.Should().Be("u-42");
        _store.Tokens.Should().NotBeNull();
        _store.Tokens!.Session.Should().Be("sess-1");
    }

    [Fact]
    public async Task SignInAsync_WhenUserCancels_ReturnsFalseAndStaysSignedOut()
    {
        _signIn.SignInAsync(Arg.Any<CancellationToken>()).Returns((GoogleAuthCode?)null);

        var svc = NewService();
        var ok = await svc.SignInAsync();

        ok.Should().BeFalse();
        svc.IsSignedIn.Should().BeFalse();
        _store.Tokens.Should().BeNull();
    }

    [Fact]
    public async Task SignOut_ClearsStoredTokensAndEntitlement()
    {
        _signIn.SignInAsync(Arg.Any<CancellationToken>()).Returns(Code);
        _api.AuthWithCodeAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Session());
        var svc = NewService();
        await svc.SignInAsync();

        svc.SignOut();

        svc.IsSignedIn.Should().BeFalse();
        svc.IsPaid.Should().BeFalse();
        _store.Tokens.Should().BeNull();
    }

    [Fact]
    public void LoadsExistingTokensFromStoreOnConstruction()
    {
        _store.Tokens = new OnlineTokens("s", "r", _now.AddHours(1), "u-7");

        var svc = NewService();

        svc.IsSignedIn.Should().BeTrue();
        svc.Uid.Should().Be("u-7");
    }

    [Fact]
    public async Task ExecuteAuthed_UsesStoredSession_WhenNotExpired()
    {
        _store.Tokens = new OnlineTokens("good-session", "r", _now.AddMinutes(30), "u-1");
        var svc = NewService();

        var used = await svc.ExecuteAuthedAsync((s, _) => Task.FromResult(s));

        used.Should().Be("good-session");
        await _api.DidNotReceiveWithAnyArgs().RefreshSessionAsync(default!, default);
    }

    [Fact]
    public async Task ExecuteAuthed_RefreshesSession_WhenExpired()
    {
        _store.Tokens = new OnlineTokens("stale", "refresh-1", _now.AddMinutes(-5), "u-1"); // already expired
        _api.RefreshSessionAsync("refresh-1", Arg.Any<CancellationToken>())
            .Returns(new RefreshedSession("fresh-session", 3600));
        var svc = NewService();

        var used = await svc.ExecuteAuthedAsync((s, _) => Task.FromResult(s));

        used.Should().Be("fresh-session");
        _store.Tokens!.Session.Should().Be("fresh-session");
    }

    [Fact]
    public async Task ExecuteAuthed_RetriesOnceOn401_ThenSucceeds()
    {
        _store.Tokens = new OnlineTokens("s1", "refresh-1", _now.AddMinutes(30), "u-1");
        _api.RefreshSessionAsync("refresh-1", Arg.Any<CancellationToken>())
            .Returns(new RefreshedSession("s2", 3600));
        var svc = NewService();

        var calls = 0;
        var result = await svc.ExecuteAuthedAsync((s, _) =>
        {
            calls++;
            if (calls == 1)
                throw new OnlineApiException(HttpStatusCode.Unauthorized, "invalid session");
            return Task.FromResult(s);
        });

        calls.Should().Be(2);
        result.Should().Be("s2");
    }

    [Fact]
    public async Task RefreshAccount_SetsIsPaidFromMe()
    {
        _store.Tokens = new OnlineTokens("s", "r", _now.AddMinutes(30), "u-1");
        _api.GetMeAsync("s", Arg.Any<CancellationToken>()).Returns(
            new MeResponse("u-1", "a@b.com", new SubscriptionInfo("active", "2027-01-01T00:00:00.000Z"), true, 1024));
        var svc = NewService();

        var me = await svc.RefreshAccountAsync();

        me.Should().NotBeNull();
        svc.IsPaid.Should().BeTrue();
        svc.SubscriptionStatus.Should().Be("active");
        svc.CurrentPeriodEndUtc.Should().NotBeNull();
    }

    [Fact]
    public async Task RefreshAccount_WhenNotSignedIn_ReturnsNullAndNotPaid()
    {
        var svc = NewService();

        (await svc.RefreshAccountAsync()).Should().BeNull();
        svc.IsPaid.Should().BeFalse();
    }

    [Fact]
    public async Task RefreshAccount_WhenAuthFullyRejected_SignsOut()
    {
        _store.Tokens = new OnlineTokens("s", "refresh-1", _now.AddMinutes(-5), "u-1"); // expired -> refresh attempted
        _api.RefreshSessionAsync("refresh-1", Arg.Any<CancellationToken>())
            .ThrowsAsync(new OnlineApiException(HttpStatusCode.Unauthorized, "invalid refresh token"));
        var svc = NewService();

        var me = await svc.RefreshAccountAsync();

        me.Should().BeNull();
        svc.IsSignedIn.Should().BeFalse();
        _store.Tokens.Should().BeNull();
    }

    private sealed class InMemoryTokenStore : IOnlineTokenStore
    {
        public OnlineTokens? Tokens;
        public OnlineTokens? Load() => Tokens;
        public void Save(OnlineTokens tokens) => Tokens = tokens;
        public void Clear() => Tokens = null;
    }
}
