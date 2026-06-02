using System.Net;
using FluentAssertions;
using Kapture.Models;
using Kapture.Services;
using Kapture.Services.CloudSync.Online;
using NSubstitute;
using Xunit;

namespace KaptureVault.Tests.Services.CloudSync;

/// <summary>
/// Phase 3 slice F — the client screenshot sync pipeline. Uses a REAL OnlineAccountService seeded
/// signed-in (substituted token store, fixed clock so no refresh) and a REAL EncryptionService (temp
/// dir, configured so EncryptBytes works and the uploaded bytes can be decrypted back), a mocked API
/// client, a fake image codec (so no real images are needed), a mocked DB, and a stub R2 endpoint
/// that captures PUT bodies. No network, no live backend, no SkiaSharp.
/// </summary>
[Collection("ScreenshotDirectory")] // serialized with CaptureEntryTests: both mutate the static ScreenshotDirectory
public sealed class ScreenshotSyncServiceTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"kv-shotsync-{Guid.NewGuid():N}");
    private readonly string _originalScreenshotDir = CaptureEntry.ScreenshotDirectory;

    public ScreenshotSyncServiceTests() => Directory.CreateDirectory(_root);

    public void Dispose()
    {
        CaptureEntry.ScreenshotDirectory = _originalScreenshotDir; // restore the global (restore tests set it)
        try { Directory.Delete(_root, recursive: true); } catch { /* best effort */ }
    }

    // ── Harness ────────────────────────────────────────────────────────────────

    private sealed class Harness
    {
        public required ScreenshotSyncService Service { get; init; }
        public required IKaptureOnlineApiClient Api { get; init; }
        public required StubR2 R2 { get; init; }
        public required IEncryptionService Enc { get; init; }
        public required FakeCodec Codec { get; init; }
    }

    private Harness Make(
        long quota,
        IReadOnlyList<CaptureEntry> dbEntries,
        IReadOnlyList<VaultObject> remoteObjects,
        bool signedIn = true,
        bool encrypted = true)
    {
        var api = Substitute.For<IKaptureOnlineApiClient>();

        var store = Substitute.For<IOnlineTokenStore>();
        if (signedIn)
            store.Load().Returns(new OnlineTokens("sess", "refresh", new DateTime(2030, 1, 1, 0, 0, 0, DateTimeKind.Utc), "u-1"));
        var account = new OnlineAccountService(
            api, Substitute.For<IGoogleSignIn>(), store, () => new DateTime(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc));

        // /me drives RefreshAccountAsync → QuotaBytes used by the client pre-check.
        api.GetMeAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(
            new MeResponse("u-1", "a@b.com", new SubscriptionInfo("active", "2027-01-01T00:00:00Z"),
                Entitled: true, StorageUsed: 0, Tier: "paid", Features: new OnlineFeatures(true, true), Quota: quota, Used: 0));

        api.ListObjectsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new VaultObjectList(remoteObjects));

        // Presigned PUT/GET URLs that encode the key, so the stub can map bodies/payloads to keys.
        api.GetObjectPutUrlAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(ci => new PresignedUrl($"https://r2.test/put?key={Uri.EscapeDataString(ci.ArgAt<string>(1))}", 300));
        api.GetObjectGetUrlAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(ci => new PresignedUrl($"https://r2.test/get?key={Uri.EscapeDataString(ci.ArgAt<string>(1))}", 300));

        // A remote vault.db meta exists, so the meta re-commit (server quota backstop) proceeds.
        api.GetVaultMetaAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new VaultMetaResult(true, new VaultMeta("2026-06-01T12:00:00Z", "sha", 10, 2, "PBKDF2-SHA256", 600_000, "salt")));

        var enc = new EncryptionService(Path.Combine(_root, "enc"));
        if (encrypted) enc.Configure("pw");

        var db = Substitute.For<IDatabaseService>();
        db.GetAll(Arg.Any<int?>()).Returns(dbEntries.ToList());

        var codec = new FakeCodec();
        var service = new ScreenshotSyncService(account, api, new HttpClient(new StubR2().Capture(out var r2)), enc, codec, db,
            () => new DateTime(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc));

        return new Harness { Service = service, Api = api, R2 = r2, Enc = enc, Codec = codec };
    }

    private CaptureEntry Screenshot(string filename, int capturedHour, byte[]? bytes = null, DateTime? expiresAt = null, bool createFile = true)
    {
        var path = Path.Combine(_root, filename);
        if (createFile) File.WriteAllBytes(path, bytes ?? [1, 2, 3]);
        return new CaptureEntry
        {
            EntryType = "screenshot",
            Content = path,
            CapturedAt = new DateTime(2026, 6, 1, capturedHour, 0, 0, DateTimeKind.Utc),
            ExpiresAt = expiresAt,
        };
    }

    private static string Key(string filename) => $"screenshots/{filename}.enc";

    // ── Tests ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task SyncUpAsync_WhenNotSignedIn_DoesNothing()
    {
        var h = Make(quota: long.MaxValue, dbEntries: [], remoteObjects: [], signedIn: false);

        var result = await h.Service.SyncUpAsync();

        result.Ran.Should().BeFalse();
        await h.Api.DidNotReceiveWithAnyArgs().ListObjectsAsync(default!, default);
    }

    [Fact]
    public async Task SyncUpAsync_WhenVaultNotEncrypted_DoesNothing()
    {
        // The Online Vault is end-to-end encrypted — with no vault password there is no upload path.
        var h = Make(quota: long.MaxValue, dbEntries: [Screenshot("sc_a.bmp", 9)], remoteObjects: [], encrypted: false);

        var result = await h.Service.SyncUpAsync();

        result.Ran.Should().BeFalse();
        await h.Api.DidNotReceiveWithAnyArgs().GetObjectPutUrlAsync(default!, default!, default);
    }

    [Fact]
    public async Task SyncUpAsync_UploadsOnlyNewScreenshots_EncryptedReEncoded()
    {
        // A is already on R2; only B should be uploaded, as the encrypted re-encoded blob.
        var a = Screenshot("sc_a.bmp", 9);
        var b = Screenshot("sc_b.bmp", 10, bytes: [7, 7, 7]);
        var h = Make(quota: long.MaxValue, dbEntries: [a, b], remoteObjects: [new VaultObject(Key("sc_a.bmp"), 100)]);

        var result = await h.Service.SyncUpAsync();

        result.Ran.Should().BeTrue();
        result.Uploaded.Should().Be(1);
        result.OrphansDeleted.Should().Be(0);
        // Exactly one upload, for B's key.
        h.R2.Puts.Should().ContainSingle();
        var body = h.R2.BodyForKey(Key("sc_b.bmp"));
        body.Should().NotBeNull();
        // The uploaded bytes decrypt back to the codec's PNG output for B's source bytes.
        h.Enc.DecryptBytes(body!).Should().Equal(FakeCodec.Png([7, 7, 7]));
        // A was not re-uploaded, nor deleted (it is still referenced).
        await h.Api.DidNotReceive().GetObjectPutUrlAsync(Arg.Any<string>(), Key("sc_a.bmp"), Arg.Any<CancellationToken>());
        await h.Api.DidNotReceiveWithAnyArgs().DeleteObjectAsync(default!, default!, default);
    }

    [Fact]
    public async Task SyncUpAsync_DeletesOrphanedRemoteScreenshots()
    {
        // Remote has A and B; the DB only references A → B is an orphan and must be deleted.
        var a = Screenshot("sc_a.bmp", 9);
        var h = Make(quota: long.MaxValue, dbEntries: [a],
            remoteObjects: [new VaultObject(Key("sc_a.bmp"), 100), new VaultObject(Key("sc_b.bmp"), 200)]);

        var result = await h.Service.SyncUpAsync();

        result.OrphansDeleted.Should().Be(1);
        result.Uploaded.Should().Be(0);
        await h.Api.Received(1).DeleteObjectAsync(Arg.Any<string>(), Key("sc_b.bmp"), Arg.Any<CancellationToken>());
        await h.Api.DidNotReceive().DeleteObjectAsync(Arg.Any<string>(), Key("sc_a.bmp"), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SyncUpAsync_NoChanges_DoesNotRecommitMeta()
    {
        // Everything desired is already on R2 and nothing is orphaned → no upload, no meta re-commit.
        var a = Screenshot("sc_a.bmp", 9);
        var h = Make(quota: long.MaxValue, dbEntries: [a], remoteObjects: [new VaultObject(Key("sc_a.bmp"), 100)]);

        var result = await h.Service.SyncUpAsync();

        result.Uploaded.Should().Be(0);
        result.OrphansDeleted.Should().Be(0);
        await h.Api.DidNotReceiveWithAnyArgs().PutVaultMetaAsync(default!, default!, default);
    }

    [Fact]
    public async Task SyncUpAsync_RecommitsMeta_AfterUploads()
    {
        var a = Screenshot("sc_a.bmp", 9);
        var h = Make(quota: long.MaxValue, dbEntries: [a], remoteObjects: []);

        await h.Service.SyncUpAsync();

        // The vault meta is re-committed so the server re-sums all objects and banks storage_used.
        await h.Api.Received(1).PutVaultMetaAsync(Arg.Any<string>(), Arg.Any<VaultMeta>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SyncUpAsync_ExcludesExpiredAndMissingFiles()
    {
        var valid = Screenshot("sc_valid.bmp", 9);
        var expired = Screenshot("sc_expired.bmp", 8, expiresAt: new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc));
        var missing = Screenshot("sc_missing.bmp", 7, createFile: false);
        var h = Make(quota: long.MaxValue, dbEntries: [valid, expired, missing], remoteObjects: []);

        var result = await h.Service.SyncUpAsync();

        result.Uploaded.Should().Be(1);
        h.R2.BodyForKey(Key("sc_valid.bmp")).Should().NotBeNull();
        h.R2.BodyForKey(Key("sc_expired.bmp")).Should().BeNull();
        h.R2.BodyForKey(Key("sc_missing.bmp")).Should().BeNull();
    }

    [Fact]
    public async Task SyncUpAsync_QuotaPreCheck_UploadsOldestFirstUntilFull()
    {
        // Each 3-byte file → 4-byte PNG → 32-byte encrypted blob (nonce 12 + tag 16 + 4). Quota 40
        // admits only the first (oldest) of three; the other two are reported as not-synced.
        var a = Screenshot("sc_a.bmp", 9);
        var b = Screenshot("sc_b.bmp", 10);
        var c = Screenshot("sc_c.bmp", 11);
        var h = Make(quota: 40, dbEntries: [c, a, b], remoteObjects: []); // deliberately out of order

        var result = await h.Service.SyncUpAsync();

        result.Uploaded.Should().Be(1);
        result.NotSyncedOverQuota.Should().Be(2);
        // The OLDEST (A, 09:00) is the one that got in.
        h.R2.BodyForKey(Key("sc_a.bmp")).Should().NotBeNull();
        h.R2.BodyForKey(Key("sc_b.bmp")).Should().BeNull();
        h.R2.BodyForKey(Key("sc_c.bmp")).Should().BeNull();
    }

    [Fact]
    public async Task SyncUpAsync_OverQuotaAtCommit_TrimsNewestAndRetries()
    {
        // Both fit the client pre-check, but the server 413s once (a concurrent device raced us over).
        // The pipeline trims the NEWEST upload (B) and retries the commit successfully.
        var a = Screenshot("sc_a.bmp", 9);
        var b = Screenshot("sc_b.bmp", 10);
        var h = Make(quota: long.MaxValue, dbEntries: [a, b], remoteObjects: []);

        var putMetaCalls = 0;
        h.Api.PutVaultMetaAsync(Arg.Any<string>(), Arg.Any<VaultMeta>(), Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                putMetaCalls++;
                return putMetaCalls == 1
                    ? throw new OnlineApiException(HttpStatusCode.RequestEntityTooLarge, "vault exceeds your storage quota")
                    : Task.CompletedTask;
            });

        var result = await h.Service.SyncUpAsync();

        result.Uploaded.Should().Be(1);          // A survived
        result.NotSyncedOverQuota.Should().Be(1); // B was trimmed
        await h.Api.Received(1).DeleteObjectAsync(Arg.Any<string>(), Key("sc_b.bmp"), Arg.Any<CancellationToken>());
        await h.Api.DidNotReceive().DeleteObjectAsync(Arg.Any<string>(), Key("sc_a.bmp"), Arg.Any<CancellationToken>());
        putMetaCalls.Should().Be(2); // failed once (413), retried once (ok)
    }

    [Fact]
    public async Task SyncUpAsync_WhenNoRemoteMetaYet_UploadsButSkipsCommit()
    {
        // First-ever sync edge: screenshots are uploaded before any vault.db.meta exists. The commit
        // step must no-op cleanly (no exception); the next sync re-commits once the meta is present.
        var a = Screenshot("sc_a.bmp", 9);
        var h = Make(quota: long.MaxValue, dbEntries: [a], remoteObjects: []);
        h.Api.GetVaultMetaAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new VaultMetaResult(false, null));

        var result = await h.Service.SyncUpAsync();

        result.Ran.Should().BeTrue();
        result.Uploaded.Should().Be(1);
        h.R2.BodyForKey(Key("sc_a.bmp")).Should().NotBeNull();
        await h.Api.DidNotReceiveWithAnyArgs().PutVaultMetaAsync(default!, default!, default);
    }

    [Fact]
    public async Task SyncUpAsync_RecommitsMeta_PreservingKdfFields()
    {
        // The meta re-commit must round-trip the remote meta verbatim — including the KDF params the
        // web vault relies on (slice A) — not strip them.
        var a = Screenshot("sc_a.bmp", 9);
        var h = Make(quota: long.MaxValue, dbEntries: [a], remoteObjects: []);

        await h.Service.SyncUpAsync();

        await h.Api.Received(1).PutVaultMetaAsync(
            Arg.Any<string>(),
            Arg.Is<VaultMeta>(m =>
                m.Kdf == "PBKDF2-SHA256" && m.Iterations == 600_000 && m.Salt == "salt" && m.Version == 2),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SyncUpAsync_SkipsCorruptScreenshot_WithoutFailingTheSync()
    {
        // The codec throws on the corrupt file (sentinel 0xFF); the valid one still uploads.
        var corrupt = Screenshot("sc_corrupt.bmp", 9, bytes: [0xFF]);
        var valid = Screenshot("sc_valid.bmp", 10, bytes: [1, 2, 3]);
        var h = Make(quota: long.MaxValue, dbEntries: [corrupt, valid], remoteObjects: []);

        var result = await h.Service.SyncUpAsync();

        result.Ran.Should().BeTrue();
        result.Uploaded.Should().Be(1);
        h.R2.BodyForKey(Key("sc_valid.bmp")).Should().NotBeNull();
        h.R2.BodyForKey(Key("sc_corrupt.bmp")).Should().BeNull();
    }

    // ── Restore (slice G) ───────────────────────────────────────────────────────

    [Fact]
    public async Task RestoreAsync_NotRun_WhenNotSignedIn()
    {
        var h = Make(quota: long.MaxValue, dbEntries: [Screenshot("sc_a.bmp", 9, createFile: false)],
            remoteObjects: [], signedIn: false);

        var result = await h.Service.RestoreAsync();

        result.Ran.Should().BeFalse();
        await h.Api.DidNotReceiveWithAnyArgs().ListObjectsAsync(default!, default);
    }

    [Fact]
    public async Task RestoreAsync_NotRun_WhenVaultNotEncrypted()
    {
        var h = Make(quota: long.MaxValue, dbEntries: [Screenshot("sc_a.bmp", 9, createFile: false)],
            remoteObjects: [], encrypted: false);

        var result = await h.Service.RestoreAsync();

        result.Ran.Should().BeFalse();
    }

    [Fact]
    public async Task RestoreAsync_DownloadsDecryptsAndWritesMissingScreenshots()
    {
        // The DB row's Content is another device's path (no local file); restore must fetch the
        // encrypted object, decrypt it, and write the image into the local screenshots dir by filename.
        var restoreDir = Path.Combine(_root, "restored");
        CaptureEntry.ScreenshotDirectory = restoreDir;
        var h = Make(quota: long.MaxValue, dbEntries: [Screenshot("sc_a.bmp", 9, createFile: false)],
            remoteObjects: [new VaultObject(Key("sc_a.bmp"), 50)]);
        var pngBytes = new byte[] { 9, 9, 9, 9 };
        h.R2.GetPayloads[Key("sc_a.bmp")] = h.Enc.EncryptBytes(pngBytes);

        var result = await h.Service.RestoreAsync();

        result.Ran.Should().BeTrue();
        result.Restored.Should().Be(1);
        var written = Path.Combine(restoreDir, "sc_a.bmp");
        File.Exists(written).Should().BeTrue();
        (await File.ReadAllBytesAsync(written)).Should().Equal(pngBytes); // decrypted back to the plaintext image
    }

    [Fact]
    public async Task RestoreAsync_SkipsScreenshotsAlreadyPresentLocally()
    {
        var restoreDir = Path.Combine(_root, "restored2");
        Directory.CreateDirectory(restoreDir);
        CaptureEntry.ScreenshotDirectory = restoreDir;
        await File.WriteAllBytesAsync(Path.Combine(restoreDir, "sc_a.bmp"), [1]); // already have it
        var h = Make(quota: long.MaxValue, dbEntries: [Screenshot("sc_a.bmp", 9, createFile: false)],
            remoteObjects: [new VaultObject(Key("sc_a.bmp"), 50)]);

        var result = await h.Service.RestoreAsync();

        result.Restored.Should().Be(0);
        await h.Api.DidNotReceiveWithAnyArgs().GetObjectGetUrlAsync(default!, default!, default);
    }

    [Fact]
    public async Task RestoreAsync_CountsScreenshotsMissingFromServer()
    {
        // Referenced by the DB but never synced (older capture, before screenshot sync existed) — not an error.
        var restoreDir = Path.Combine(_root, "restored3");
        CaptureEntry.ScreenshotDirectory = restoreDir;
        var h = Make(quota: long.MaxValue, dbEntries: [Screenshot("sc_a.bmp", 9, createFile: false)],
            remoteObjects: []);

        var result = await h.Service.RestoreAsync();

        result.Restored.Should().Be(0);
        result.MissingRemote.Should().Be(1);
        await h.Api.DidNotReceiveWithAnyArgs().GetObjectGetUrlAsync(default!, default!, default);
    }

    [Fact]
    public async Task RestoreAsync_SkipsUndecryptableBlob_WithoutFailing()
    {
        var restoreDir = Path.Combine(_root, "restored4");
        CaptureEntry.ScreenshotDirectory = restoreDir;
        var h = Make(quota: long.MaxValue, dbEntries: [Screenshot("sc_a.bmp", 9, createFile: false)],
            remoteObjects: [new VaultObject(Key("sc_a.bmp"), 3)]);
        h.R2.GetPayloads[Key("sc_a.bmp")] = [1, 2, 3]; // too short to be a valid blob → DecryptionException

        var result = await h.Service.RestoreAsync();

        result.Ran.Should().BeTrue();
        result.Restored.Should().Be(0);
        File.Exists(Path.Combine(restoreDir, "sc_a.bmp")).Should().BeFalse();
    }

    // ── Fakes ──────────────────────────────────────────────────────────────────

    /// <summary>Deterministic stand-in for the SkiaSharp codec: PNG = 0xAA prefix + source; throws on 0xFF.</summary>
    private sealed class FakeCodec : IScreenshotImageCodec
    {
        public static byte[] Png(byte[] source) => [0xAA, .. source];

        public byte[] ReEncodeToPng(byte[] source)
        {
            if (source.Length > 0 && source[0] == 0xFF)
                throw new InvalidOperationException("corrupt");
            return Png(source);
        }
    }

    /// <summary>Stub R2 endpoint: records every PUT (URL + body); serves GET payloads mapped by object key.</summary>
    private sealed class StubR2 : HttpMessageHandler
    {
        public List<(string Url, byte[] Body)> Puts { get; } = [];

        /// <summary>Payload returned on GET when the request URL carries this (relative) object key.</summary>
        public Dictionary<string, byte[]> GetPayloads { get; } = new(StringComparer.Ordinal);

        public StubR2 Capture(out StubR2 self) { self = this; return this; }

        public byte[]? BodyForKey(string key)
        {
            var marker = Uri.EscapeDataString(key);
            foreach (var (url, body) in Puts)
                if (url.Contains(marker, StringComparison.Ordinal))
                    return body;
            return null;
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.Method == HttpMethod.Put)
            {
                var body = request.Content is null ? [] : await request.Content.ReadAsByteArrayAsync(cancellationToken);
                Puts.Add((request.RequestUri!.AbsoluteUri, body));
                return new HttpResponseMessage(HttpStatusCode.OK);
            }

            // GET: return the payload whose key is encoded in the URL, else 404.
            var url = request.RequestUri!.AbsoluteUri;
            foreach (var (key, payload) in GetPayloads)
                if (url.Contains(Uri.EscapeDataString(key), StringComparison.Ordinal))
                    return new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(payload) };
            return new HttpResponseMessage(HttpStatusCode.NotFound);
        }
    }
}
