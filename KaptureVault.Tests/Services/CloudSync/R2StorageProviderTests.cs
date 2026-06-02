using System.Net;
using System.Text;
using FluentAssertions;
using Kapture.Services;
using Kapture.Services.CloudSync.Online;
using NSubstitute;
using Xunit;

namespace KaptureVault.Tests.Services.CloudSync;

/// <summary>
/// R2StorageProvider (F-02 Phase 2): the Online Vault as an ICloudStorageProvider. Uses a REAL
/// OnlineAccountService seeded signed-in (via a substituted token store) so the session/refresh
/// path is exercised, a mocked API client returning presigned URLs, and a stub R2 endpoint that
/// captures PUT bytes / serves GET bytes. No network, no live backend.
/// </summary>
public class R2StorageProviderTests
{
    private static (R2StorageProvider provider, IKaptureOnlineApiClient api, StubR2Handler r2, IEncryptionService enc) NewProvider()
    {
        var api = Substitute.For<IKaptureOnlineApiClient>();
        var store = Substitute.For<IOnlineTokenStore>();
        // Signed in, session valid well past the fixed clock so no refresh is triggered.
        store.Load().Returns(new OnlineTokens("sess", "refresh", new DateTime(2030, 1, 1, 0, 0, 0, DateTimeKind.Utc), "u-1"));
        var account = new OnlineAccountService(
            api, Substitute.For<IGoogleSignIn>(), store, () => new DateTime(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc));

        var enc = Substitute.For<IEncryptionService>();
        enc.IsActive.Returns(true); // default: vault encrypted/unlocked, so uploads are allowed
        var r2 = new StubR2Handler();
        var provider = new R2StorageProvider(account, api, new HttpClient(r2), enc);
        return (provider, api, r2, enc);
    }

    [Fact]
    public void ProviderName_IsOnlineVault_AndAuthReflectsAccount()
    {
        var (provider, _, _, _) = NewProvider();
        provider.ProviderName.Should().Be("Online Vault");
        provider.IsAuthenticated.Should().BeTrue();
    }

    [Fact]
    public async Task UploadFileAsync_PutsBytesToPresignedUrl_AndWritesMeta()
    {
        var (provider, api, r2, _) = NewProvider();
        api.GetVaultPutUrlAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new PresignedUrl("https://r2.test/put?sig=abc", 300));
        var bytes = Encoding.UTF8.GetBytes("encrypted-vault-bytes");
        var tmp = Path.Combine(Path.GetTempPath(), $"kvr2-up-{Guid.NewGuid():N}.db");
        await File.WriteAllBytesAsync(tmp, bytes);

        try
        {
            var id = await provider.UploadFileAsync(tmp, "vault.db");

            id.Should().Be("vault");
            r2.PutUri!.AbsoluteUri.Should().Be("https://r2.test/put?sig=abc");
            r2.PutBody.Should().Equal(bytes);
            await api.Received(1).PutVaultMetaAsync(
                Arg.Any<string>(),
                Arg.Is<VaultMeta>(m => m.Size == bytes.Length && m.Sha256.Length == 64),
                Arg.Any<CancellationToken>());
        }
        finally { File.Delete(tmp); }
    }

    [Fact]
    public async Task UploadFileAsync_RefusesWhenVaultNotEncrypted()
    {
        // Phase 3 slice B: the Online Vault is end-to-end encrypted — never upload a plaintext vault.
        // This is the authoritative backstop (the UI gates earlier). With no active vault password the
        // upload must throw and touch neither the presign API nor R2.
        var (provider, api, r2, enc) = NewProvider();
        enc.IsActive.Returns(false);
        var tmp = Path.Combine(Path.GetTempPath(), $"kvr2-noenc-{Guid.NewGuid():N}.db");
        await File.WriteAllBytesAsync(tmp, [1, 2, 3]);

        try
        {
            Func<Task> act = () => provider.UploadFileAsync(tmp, "vault.db");
            await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*vault password*");
            r2.PutUri.Should().BeNull();
            await api.DidNotReceiveWithAnyArgs().GetVaultPutUrlAsync(default!, default);
            await api.DidNotReceiveWithAnyArgs().PutVaultMetaAsync(default!, default!, default);
        }
        finally { File.Delete(tmp); }
    }

    [Fact]
    public async Task UploadFileAsync_WritesKdfParamsIntoMeta_ForWebUnlock()
    {
        var (provider, api, _, enc) = NewProvider();
        enc.GetKdfInfo().Returns(new VaultKdfInfo("PBKDF2-SHA256", 600_000, "c2FsdHk="));
        api.GetVaultPutUrlAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new PresignedUrl("https://r2.test/put", 300));
        var tmp = Path.Combine(Path.GetTempPath(), $"kvr2-kdf-{Guid.NewGuid():N}.db");
        await File.WriteAllBytesAsync(tmp, [1, 2, 3, 4]);

        try
        {
            await provider.UploadFileAsync(tmp, "vault.db");

            // The meta must carry the public KDF params so the web vault / a 2nd device can derive the key.
            await api.Received(1).PutVaultMetaAsync(
                Arg.Any<string>(),
                Arg.Is<VaultMeta>(m =>
                    m.Kdf == "PBKDF2-SHA256" && m.Iterations == 600_000 && m.Salt == "c2FsdHk=" && m.Version == 2),
                Arg.Any<CancellationToken>());
        }
        finally { File.Delete(tmp); }
    }

    [Fact]
    public async Task DownloadFileAsync_GetsBytesFromPresignedUrl_AndWritesFile()
    {
        var (provider, api, r2, _) = NewProvider();
        var bytes = Encoding.UTF8.GetBytes("downloaded-vault-bytes");
        r2.GetPayload = bytes;
        api.GetVaultGetUrlAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new PresignedUrl("https://r2.test/get?sig=xyz", 300));
        var outPath = Path.Combine(Path.GetTempPath(), $"kvr2-down-{Guid.NewGuid():N}.db");

        try
        {
            var ok = await provider.DownloadFileAsync("vault", outPath);

            ok.Should().BeTrue();
            (await File.ReadAllBytesAsync(outPath)).Should().Equal(bytes);
        }
        finally { if (File.Exists(outPath)) File.Delete(outPath); }
    }

    [Fact]
    public async Task FindFileAsync_ReturnsVaultId_WhenRemoteExists_ElseNull()
    {
        var (provider, api, _, _) = NewProvider();
        api.GetVaultMetaAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(
                new VaultMetaResult(true, new VaultMeta("2026-06-01T12:00:00.000Z", "sha", 10, 1)),
                new VaultMetaResult(false, null));

        (await provider.FindFileAsync("vault.db")).Should().Be("vault");
        (await provider.FindFileAsync("vault.db")).Should().BeNull();
    }

    [Fact]
    public async Task GetRemoteModifiedTimeAsync_ReturnsParsedMtime()
    {
        var (provider, api, _, _) = NewProvider();
        api.GetVaultMetaAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new VaultMetaResult(true, new VaultMeta("2026-06-01T12:00:00.000Z", "sha", 10, 1)));

        var t = await provider.GetRemoteModifiedTimeAsync("vault");

        t.Should().NotBeNull();
        t!.Value.ToUniversalTime().Should().Be(new DateTime(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public async Task UploadFileAsync_SurfacesR2Failure()
    {
        var (provider, api, r2, _) = NewProvider();
        r2.PutStatus = HttpStatusCode.Forbidden;
        api.GetVaultPutUrlAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new PresignedUrl("https://r2.test/put", 300));
        var tmp = Path.Combine(Path.GetTempPath(), $"kvr2-fail-{Guid.NewGuid():N}.db");
        await File.WriteAllBytesAsync(tmp, [1, 2, 3]);

        try
        {
            Func<Task> act = () => provider.UploadFileAsync(tmp, "vault.db");
            await act.Should().ThrowAsync<InvalidOperationException>();
        }
        finally { File.Delete(tmp); }
    }

    /// <summary>Stub R2 endpoint: captures the PUT body/URI; serves a configured payload on GET.</summary>
    private sealed class StubR2Handler : HttpMessageHandler
    {
        public byte[]? PutBody { get; private set; }
        public Uri? PutUri { get; private set; }
        public byte[] GetPayload { get; set; } = [];
        public HttpStatusCode PutStatus { get; set; } = HttpStatusCode.OK;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.Method == HttpMethod.Put)
            {
                PutUri = request.RequestUri;
                PutBody = request.Content is null ? [] : await request.Content.ReadAsByteArrayAsync(cancellationToken);
                return new HttpResponseMessage(PutStatus);
            }
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(GetPayload) };
        }
    }
}
