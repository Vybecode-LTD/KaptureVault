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
    public async Task AuthWithCodeAsync_PostsPkceCodeAndVerifier_AndParsesSession()
    {
        var (client, handler) = Make((_, _) =>
            Json(HttpStatusCode.OK, """{"session":"s.jwt","refresh":"r.jwt","uid":"u-9","expiresIn":3600}"""));

        var result = await client.AuthWithCodeAsync("auth-code-abc", "verifier-123", "http://localhost:48722/");

        handler.LastRequest!.Method.Should().Be(HttpMethod.Post);
        handler.LastRequest.RequestUri!.AbsoluteUri.Should().Be($"{Base}/auth/google");
        handler.LastBody.Should().Contain("auth-code-abc")
            .And.Contain("code_verifier").And.Contain("verifier-123")
            .And.Contain("redirect_uri");
        result.Uid.Should().Be("u-9");
        result.Refresh.Should().Be("r.jwt");
    }

    [Fact]
    public async Task CreateHandoffCodeAsync_PostsWithBearer_AndParsesCode()
    {
        var (client, handler) = Make((_, _) =>
            Json(HttpStatusCode.OK, """{"code":"handoff-abc123","expiresIn":120}"""));

        var result = await client.CreateHandoffCodeAsync("sess-123");

        handler.LastRequest!.Method.Should().Be(HttpMethod.Post);
        handler.LastRequest.RequestUri!.AbsoluteUri.Should().Be($"{Base}/auth/handoff/create");
        handler.LastRequest.Headers.Authorization!.Parameter.Should().Be("sess-123");
        result.Code.Should().Be("handoff-abc123");
        result.ExpiresIn.Should().Be(120);
    }

    [Fact]
    public async Task PutVaultMetaAsync_PutsMetaJson_WithBearer()
    {
        var (client, handler) = Make((_, _) => Json(HttpStatusCode.OK, """{"ok":true}"""));

        await client.PutVaultMetaAsync("sess-123", new VaultMeta("2026-06-01T12:00:00.000Z", "shahash", 2048, 1));

        handler.LastRequest!.Method.Should().Be(HttpMethod.Put);
        handler.LastRequest.RequestUri!.AbsoluteUri.Should().Be($"{Base}/vault/meta");
        handler.LastRequest.Headers.Authorization!.Parameter.Should().Be("sess-123");
        handler.LastBody.Should().Contain("mtime").And.Contain("sha256").And.Contain("2048");
    }

    [Fact]
    public async Task PutVaultMetaAsync_WhenNotEntitled_ThrowsPaymentRequired()
    {
        var (client, _) = Make((_, _) => Json(HttpStatusCode.PaymentRequired, """{"error":"subscription required"}"""));

        Func<Task> act = () => client.PutVaultMetaAsync("sess", new VaultMeta("t", "s", 1, 1));

        (await act.Should().ThrowAsync<OnlineApiException>()).Which.IsPaymentRequired.Should().BeTrue();
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

    // ── Vault object API (Phase 3 slice F client side) ─────────────────────────

    [Fact]
    public async Task GetObjectPutUrlAsync_PostsKeyWithBearer_AndParsesPresignedUrl()
    {
        var (client, handler) = Make((_, _) =>
            Json(HttpStatusCode.OK, """{"url":"https://r2.test/put?X-Amz-Signature=abc","expiresIn":300}"""));

        var presigned = await client.GetObjectPutUrlAsync("sess-123", "screenshots/sc_1.bmp.enc");

        handler.LastRequest!.Method.Should().Be(HttpMethod.Post);
        handler.LastRequest.RequestUri!.AbsoluteUri.Should().Be($"{Base}/vault/object/put-url");
        handler.LastRequest.Headers.Authorization!.Parameter.Should().Be("sess-123");
        handler.LastBody.Should().Contain("key").And.Contain("screenshots/sc_1.bmp.enc");
        presigned.Url.Should().Contain("X-Amz-Signature=");
        presigned.ExpiresIn.Should().Be(300);
    }

    [Fact]
    public async Task GetObjectGetUrlAsync_PostsKeyWithBearer_AndParsesPresignedUrl()
    {
        var (client, handler) = Make((_, _) =>
            Json(HttpStatusCode.OK, """{"url":"https://r2.test/get?X-Amz-Signature=xyz","expiresIn":300}"""));

        var presigned = await client.GetObjectGetUrlAsync("sess-123", "screenshots/sc_2.bmp.enc");

        handler.LastRequest!.RequestUri!.AbsoluteUri.Should().Be($"{Base}/vault/object/get-url");
        handler.LastBody.Should().Contain("screenshots/sc_2.bmp.enc");
        presigned.Url.Should().Contain("X-Amz-Signature=");
    }

    [Fact]
    public async Task DeleteObjectAsync_PostsKeyWithBearer()
    {
        var (client, handler) = Make((_, _) => Json(HttpStatusCode.OK, """{"ok":true}"""));

        await client.DeleteObjectAsync("sess-123", "screenshots/sc_3.bmp.enc");

        handler.LastRequest!.Method.Should().Be(HttpMethod.Post);
        handler.LastRequest.RequestUri!.AbsoluteUri.Should().Be($"{Base}/vault/object/delete");
        handler.LastRequest.Headers.Authorization!.Parameter.Should().Be("sess-123");
        handler.LastBody.Should().Contain("screenshots/sc_3.bmp.enc");
    }

    [Fact]
    public async Task ListObjectsAsync_GetsWithBearer_AndParsesObjects()
    {
        var (client, handler) = Make((_, _) => Json(HttpStatusCode.OK,
            """{"objects":[{"key":"vault.db","size":4096},{"key":"screenshots/sc_1.bmp.enc","size":2048}]}"""));

        var result = await client.ListObjectsAsync("sess-123");

        handler.LastRequest!.Method.Should().Be(HttpMethod.Get);
        handler.LastRequest.RequestUri!.AbsoluteUri.Should().Be($"{Base}/vault/objects");
        handler.LastRequest.Headers.Authorization!.Parameter.Should().Be("sess-123");
        result.Objects.Should().HaveCount(2);
        result.Objects[0].Key.Should().Be("vault.db");
        result.Objects[0].Size.Should().Be(4096);
        result.Objects[1].Key.Should().Be("screenshots/sc_1.bmp.enc");
        result.Objects[1].Size.Should().Be(2048);
    }

    [Fact]
    public async Task PutVaultMetaAsync_WhenOverQuota_ThrowsPayloadTooLarge()
    {
        // Phase 3 slice E backstop: the meta commit sums all vault objects and 413s when over quota.
        var (client, _) = Make((_, _) =>
            Json(HttpStatusCode.RequestEntityTooLarge, """{"error":"vault exceeds your storage quota","used":99,"quota":50}"""));

        Func<Task> act = () => client.PutVaultMetaAsync("sess", new VaultMeta("t", "s", 1, 1));

        var ex = (await act.Should().ThrowAsync<OnlineApiException>()).Which;
        ex.IsPayloadTooLarge.Should().BeTrue();
        ex.IsPaymentRequired.Should().BeFalse();
    }

    // ── Hosted files (Phase 6) ─────────────────────────────────────────────────

    [Fact]
    public async Task CreateFilePutUrlAsync_PostsNameAndSize_WithBearer_AndParsesTicket()
    {
        var (client, handler) = Make((_, _) =>
            Json(HttpStatusCode.OK, """{"id":"file-1","name":"a.pdf","url":"https://r2/put?X-Amz-Signature=s","expiresIn":300}"""));

        var t = await client.CreateFilePutUrlAsync("sess-1", "a.pdf", 4096, "application/pdf");

        handler.LastRequest!.Method.Should().Be(HttpMethod.Post);
        handler.LastRequest.RequestUri!.AbsoluteUri.Should().Be($"{Base}/files/put-url");
        handler.LastRequest.Headers.Authorization!.Parameter.Should().Be("sess-1");
        handler.LastBody.Should().Contain("a.pdf").And.Contain("4096");
        t.Id.Should().Be("file-1");
        t.Url.Should().Contain("X-Amz-Signature=");
    }

    [Fact]
    public async Task ListFilesAsync_GetsWithBearer_AndParsesFiles()
    {
        var (client, handler) = Make((_, _) => Json(HttpStatusCode.OK,
            """{"files":[{"id":"f1","name":"a.pdf","size":4096,"contentType":"application/pdf","createdAt":"2026-06-02"}]}"""));

        var list = await client.ListFilesAsync("sess-1");

        handler.LastRequest!.Method.Should().Be(HttpMethod.Get);
        handler.LastRequest.RequestUri!.AbsoluteUri.Should().Be($"{Base}/files");
        list.Files.Should().ContainSingle(f => f.Id == "f1" && f.Size == 4096);
    }

    [Fact]
    public async Task CreateShareAsync_PostsToTheFileShareRoute_AndParsesTheLink()
    {
        var (client, handler) = Make((_, _) => Json(HttpStatusCode.OK, """{"token":"tok123","url":"https://api/s/tok123"}"""));

        var share = await client.CreateShareAsync("sess-1", "f1", null);

        handler.LastRequest!.Method.Should().Be(HttpMethod.Post);
        handler.LastRequest.RequestUri!.AbsoluteUri.Should().Be($"{Base}/files/f1/share");
        share.Token.Should().Be("tok123");
        share.Url.Should().Contain("/s/tok123");
    }

    [Fact]
    public async Task DeleteFileAsync_SendsDelete_WithBearer()
    {
        var (client, handler) = Make((_, _) => Json(HttpStatusCode.OK, """{"ok":true}"""));

        await client.DeleteFileAsync("sess-1", "f1");

        handler.LastRequest!.Method.Should().Be(HttpMethod.Delete);
        handler.LastRequest.RequestUri!.AbsoluteUri.Should().Be($"{Base}/files/f1");
        handler.LastRequest.Headers.Authorization!.Parameter.Should().Be("sess-1");
    }

    [Fact]
    public async Task CreateFilePutUrlAsync_WhenNotEntitled_ThrowsPaymentRequired()
    {
        var (client, _) = Make((_, _) => Json(HttpStatusCode.PaymentRequired, """{"error":"subscription required"}"""));

        Func<Task> act = () => client.CreateFilePutUrlAsync("sess", "a", 1, null);

        (await act.Should().ThrowAsync<OnlineApiException>()).Which.IsPaymentRequired.Should().BeTrue();
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
