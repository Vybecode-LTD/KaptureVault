using System.Net;
using System.Text;
using FluentAssertions;
using Kapture.Services.CloudSync.Online;
using Xunit;

namespace KaptureVault.Tests.Services.CloudSync;

/// <summary>
/// Pins the Online Vault HTTP contract (F-02 Phase 2): the client must send the exact request the
/// Cloudflare Worker expects and parse exactly what it returns (verified against the Worker source
/// + its vitest acceptance suite in repo kapturevault-backend). Uses a stub message handler — no
/// network, no live backend.
/// </summary>
public class KaptureOnlineApiClientTests
{
    private const string Base = "https://test.local";

    private static HttpResponseMessage Json(HttpStatusCode code, string body) =>
        new(code) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    private static (KaptureOnlineApiClient client, StubHandler handler) Make(
        Func<HttpRequestMessage, string?, HttpResponseMessage> responder)
    {
        var handler = new StubHandler(responder);
        return (new KaptureOnlineApiClient(new HttpClient(handler), Base), handler);
    }

    [Fact]
    public async Task AuthSessionAsync_PostsIdToken_AndParsesSession()
    {
        var (client, handler) = Make((_, _) =>
            Json(HttpStatusCode.OK, """{"session":"s.jwt","refresh":"r.jwt","uid":"u-1","expiresIn":3600}"""));

        var result = await client.AuthSessionAsync("google-id-token-xyz");

        handler.LastRequest!.Method.Should().Be(HttpMethod.Post);
        handler.LastRequest.RequestUri!.AbsoluteUri.Should().Be($"{Base}/auth/session");
        handler.LastBody.Should().Contain("id_token").And.Contain("google-id-token-xyz");
        result.Session.Should().Be("s.jwt");
        result.Refresh.Should().Be("r.jwt");
        result.Uid.Should().Be("u-1");
        result.ExpiresIn.Should().Be(3600);
    }

    [Fact]
    public async Task RefreshSessionAsync_PostsRefreshToken_AndParsesRotatedSession()
    {
        var (client, handler) = Make((_, _) =>
            Json(HttpStatusCode.OK, """{"session":"new.jwt","expiresIn":3600}"""));

        var refreshed = await client.RefreshSessionAsync("refresh-xyz");

        handler.LastRequest!.RequestUri!.AbsoluteUri.Should().Be($"{Base}/auth/refresh");
        handler.LastBody.Should().Contain("refresh").And.Contain("refresh-xyz");
        refreshed.Session.Should().Be("new.jwt");
        refreshed.ExpiresIn.Should().Be(3600);
    }

    [Fact]
    public async Task GetMeAsync_SendsBearer_AndParsesSubscriptionAndEntitlement()
    {
        var (client, handler) = Make((_, _) => Json(HttpStatusCode.OK,
            """{"uid":"u-1","email":"a@b.com","subscription":{"status":"active","currentPeriodEnd":"2027-01-01T00:00:00.000Z"},"entitled":true,"storageUsed":1234}"""));

        var me = await client.GetMeAsync("sess-123");

        handler.LastRequest!.Method.Should().Be(HttpMethod.Get);
        handler.LastRequest.RequestUri!.AbsoluteUri.Should().Be($"{Base}/me");
        handler.LastRequest.Headers.Authorization!.Scheme.Should().Be("Bearer");
        handler.LastRequest.Headers.Authorization.Parameter.Should().Be("sess-123");
        me.Uid.Should().Be("u-1");
        me.Email.Should().Be("a@b.com");
        me.Subscription.Status.Should().Be("active");
        me.Subscription.CurrentPeriodEnd.Should().Be("2027-01-01T00:00:00.000Z");
        me.Entitled.Should().BeTrue();
        me.StorageUsed.Should().Be(1234);
    }

    [Fact]
    public async Task GetVaultPutUrlAsync_PostsWithBearer_AndParsesPresignedUrl()
    {
        var (client, handler) = Make((_, _) => Json(HttpStatusCode.OK,
            """{"url":"https://acct.r2.cloudflarestorage.com/kapturevault/users/u-1/vault/vault.db?X-Amz-Signature=abc","expiresIn":300}"""));

        var presigned = await client.GetVaultPutUrlAsync("sess-123");

        handler.LastRequest!.Method.Should().Be(HttpMethod.Post);
        handler.LastRequest.RequestUri!.AbsoluteUri.Should().Be($"{Base}/vault/put-url");
        handler.LastRequest.Headers.Authorization!.Parameter.Should().Be("sess-123");
        presigned.Url.Should().Contain("X-Amz-Signature=");
        presigned.ExpiresIn.Should().Be(300);
    }

    [Fact]
    public async Task GetVaultMetaAsync_WhenNoRemoteVault_ReturnsNotExists()
    {
        var (client, _) = Make((_, _) => Json(HttpStatusCode.OK, """{"exists":false}"""));

        var result = await client.GetVaultMetaAsync("sess-123");

        result.Exists.Should().BeFalse();
        result.Meta.Should().BeNull();
    }

    [Fact]
    public async Task GetVaultMetaAsync_WhenRemoteVaultExists_ParsesMeta()
    {
        var (client, _) = Make((_, _) => Json(HttpStatusCode.OK,
            """{"mtime":"2026-06-01T12:00:00.000Z","sha256":"deadbeef","size":4096,"version":1}"""));

        var result = await client.GetVaultMetaAsync("sess-123");

        result.Exists.Should().BeTrue();
        result.Meta!.Mtime.Should().Be("2026-06-01T12:00:00.000Z");
        result.Meta.Sha256.Should().Be("deadbeef");
        result.Meta.Size.Should().Be(4096);
    }

    [Fact]
    public async Task PaymentRequired_ThrowsOnlineApiException_WithStatusAndError()
    {
        var (client, _) = Make((_, _) => Json(HttpStatusCode.PaymentRequired, """{"error":"subscription required"}"""));

        Func<Task> act = () => client.GetVaultGetUrlAsync("sess-123");

        var ex = (await act.Should().ThrowAsync<OnlineApiException>()).Which;
        ex.StatusCode.Should().Be(HttpStatusCode.PaymentRequired);
        ex.IsPaymentRequired.Should().BeTrue();
        ex.Message.Should().Contain("subscription required");
    }

    [Fact]
    public async Task Unauthorized_SetsIsUnauthorizedOnTheException()
    {
        var (client, _) = Make((_, _) => Json(HttpStatusCode.Unauthorized, """{"error":"invalid session"}"""));

        Func<Task> act = () => client.GetMeAsync("bad-session");

        var ex = (await act.Should().ThrowAsync<OnlineApiException>()).Which;
        ex.IsUnauthorized.Should().BeTrue();
        ex.IsPaymentRequired.Should().BeFalse();
    }

    /// <summary>Records the outbound request (method, URI, headers, body) and returns a canned response.</summary>
    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, string?, HttpResponseMessage> _responder;
        public HttpRequestMessage? LastRequest { get; private set; }
        public string? LastBody { get; private set; }

        public StubHandler(Func<HttpRequestMessage, string?, HttpResponseMessage> responder) => _responder = responder;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            LastBody = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            return _responder(request, LastBody);
        }
    }
}
