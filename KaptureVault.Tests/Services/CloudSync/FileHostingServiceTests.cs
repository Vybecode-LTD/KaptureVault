using System.Net;
using FluentAssertions;
using Kapture.Services;
using Kapture.Services.CloudSync.Online;
using NSubstitute;
using Xunit;

namespace KaptureVault.Tests.Services.CloudSync;

/// <summary>
/// Phase 6 client file-hosting pipeline: UploadAsync = (optionally encrypt) → presigned PUT-url → PUT
/// the bytes straight to R2 → commit; DownloadAsync = get-url → fetch → (decrypt if private) → write;
/// plus list / delete / share. Uses the REAL OnlineAccountService (a valid in-memory token, so
/// ExecuteAuthedAsync forwards the session) over a mocked api client + a stub R2 handler — no network.
/// (NSubstitute note: these api methods have several string params, so all string args use matchers.)
/// </summary>
public class FileHostingServiceTests
{
    private readonly IKaptureOnlineApiClient _api = Substitute.For<IKaptureOnlineApiClient>();
    private readonly IGoogleSignIn _signIn = Substitute.For<IGoogleSignIn>();
    private readonly IEncryptionService _enc = Substitute.For<IEncryptionService>();
    private readonly TokenStore _store = new()
    {
        Tokens = new OnlineTokens("sess", "refresh", DateTime.UtcNow.AddHours(1), "u-1"),
    };

    private FileHostingService NewService(CapturingHandler r2) =>
        new(new OnlineAccountService(_api, _signIn, _store), _api, _enc, new HttpClient(r2));

    [Fact]
    public async Task UploadAsync_Unencrypted_PutsBytesToR2_ThenCommits()
    {
        var tmp = Path.GetTempFileName();
        await File.WriteAllBytesAsync(tmp, new byte[1234]);
        try
        {
            var name = Path.GetFileName(tmp);
            _api.CreateFilePutUrlAsync(Arg.Is("sess"), Arg.Is(name), 1234, Arg.Any<string?>(), false, Arg.Any<string?>(), Arg.Any<CancellationToken>())
                .Returns(new FileUploadTicket("file-1", name, "https://r2.test/put?X-Amz-Signature=abc", 300));
            _api.CommitFileAsync(Arg.Is("sess"), Arg.Is("file-1"), Arg.Any<string?>(), Arg.Any<CancellationToken>())
                .Returns(new FileCommitResult("file-1", 1234, 1234, 9_999_999));
            var r2 = new CapturingHandler(HttpStatusCode.OK);
            var svc = NewService(r2);

            var file = await svc.UploadAsync(tmp, encrypt: false, folder: null);

            file.Id.Should().Be("file-1");
            file.Encrypted.Should().BeFalse();
            r2.LastRequest!.Method.Should().Be(HttpMethod.Put);
            r2.LastRequest.RequestUri!.AbsoluteUri.Should().Contain("r2.test/put");
            _enc.DidNotReceiveWithAnyArgs().EncryptBytes(default!);
            await _api.Received(1).CommitFileAsync(Arg.Is("sess"), Arg.Is("file-1"), Arg.Any<string?>(), Arg.Any<CancellationToken>());
        }
        finally
        {
            File.Delete(tmp);
        }
    }

    [Fact]
    public async Task UploadAsync_Encrypted_EncryptsBeforePut_AndMarksTheFilePrivate()
    {
        var tmp = Path.GetTempFileName();
        await File.WriteAllBytesAsync(tmp, new byte[] { 1, 2, 3 });
        try
        {
            var name = Path.GetFileName(tmp);
            var cipher = new byte[] { 9, 9, 9, 9, 9 };
            _enc.IsActive.Returns(true);
            _enc.EncryptBytes(Arg.Any<byte[]>()).Returns(cipher);
            _api.CreateFilePutUrlAsync(Arg.Is("sess"), Arg.Is(name), cipher.Length, Arg.Any<string?>(), true, Arg.Any<string?>(), Arg.Any<CancellationToken>())
                .Returns(new FileUploadTicket("file-2", name, "https://r2.test/put", 300));
            _api.CommitFileAsync(Arg.Is("sess"), Arg.Is("file-2"), Arg.Any<string?>(), Arg.Any<CancellationToken>())
                .Returns(new FileCommitResult("file-2", cipher.Length, cipher.Length, 9_999_999));
            var r2 = new CapturingHandler(HttpStatusCode.OK);
            var svc = NewService(r2);

            var file = await svc.UploadAsync(tmp, encrypt: true, folder: "Docs");

            file.Encrypted.Should().BeTrue();
            file.Folder.Should().Be("Docs");
            _enc.Received(1).EncryptBytes(Arg.Any<byte[]>());
            r2.LastBody.Should().Equal(cipher); // the PUT carried CIPHERTEXT, not the plaintext
        }
        finally
        {
            File.Delete(tmp);
        }
    }

    [Fact]
    public async Task UploadAsync_Encrypted_WithoutAVaultPassword_Throws_AndDoesNotUpload()
    {
        var tmp = Path.GetTempFileName();
        await File.WriteAllBytesAsync(tmp, new byte[] { 1 });
        try
        {
            _enc.IsActive.Returns(false);
            var svc = NewService(new CapturingHandler(HttpStatusCode.OK));

            await ((Func<Task>)(() => svc.UploadAsync(tmp, encrypt: true, folder: null)))
                .Should().ThrowAsync<InvalidOperationException>();
            await _api.DidNotReceiveWithAnyArgs()
                .CreateFilePutUrlAsync(default!, default!, default, default, default, default, default);
        }
        finally
        {
            File.Delete(tmp);
        }
    }

    [Fact]
    public async Task UploadAsync_RejectsAFileOverTheCap_WithoutCallingTheApi()
    {
        var tmp = Path.GetTempFileName();
        using (var fs = File.Create(tmp)) fs.SetLength(FileHostingService.MaxFileBytesConst + 1); // sparse, no 250 MB write
        try
        {
            var svc = NewService(new CapturingHandler(HttpStatusCode.OK));

            await ((Func<Task>)(() => svc.UploadAsync(tmp, encrypt: false, folder: null)))
                .Should().ThrowAsync<InvalidOperationException>();
            await _api.DidNotReceiveWithAnyArgs()
                .CreateFilePutUrlAsync(default!, default!, default, default, default, default, default);
        }
        finally
        {
            File.Delete(tmp);
        }
    }

    [Fact]
    public async Task UploadAsync_WhenR2PutFails_Throws_AndDoesNotCommit()
    {
        var tmp = Path.GetTempFileName();
        await File.WriteAllBytesAsync(tmp, new byte[10]);
        try
        {
            _api.CreateFilePutUrlAsync(Arg.Is("sess"), Arg.Any<string>(), Arg.Any<long>(), Arg.Any<string?>(), false, Arg.Any<string?>(), Arg.Any<CancellationToken>())
                .Returns(new FileUploadTicket("file-x", "a", "https://r2.test/put", 300));
            var svc = NewService(new CapturingHandler(HttpStatusCode.Forbidden));

            await ((Func<Task>)(() => svc.UploadAsync(tmp, encrypt: false, folder: null)))
                .Should().ThrowAsync<InvalidOperationException>();
            await _api.DidNotReceiveWithAnyArgs().CommitFileAsync(default!, default!, default, default);
        }
        finally
        {
            File.Delete(tmp);
        }
    }

    [Fact]
    public async Task DownloadAsync_Encrypted_DecryptsTheBytes()
    {
        var tmp = Path.GetTempFileName();
        try
        {
            var plain = new byte[] { 1, 2, 3, 4 };
            _enc.IsActive.Returns(true);
            _enc.DecryptBytes(Arg.Any<byte[]>()).Returns(plain);
            _api.GetFileGetUrlAsync(Arg.Is("sess"), Arg.Is("file-3"), Arg.Any<CancellationToken>())
                .Returns(new PresignedUrl("https://r2.test/get", 300));
            var svc = NewService(new CapturingHandler(HttpStatusCode.OK) { ResponseBody = new byte[] { 5, 5, 5 } });
            var file = new HostedFile("file-3", "a", 3, null, Encrypted: true, Folder: null, CreatedAt: "t");

            await svc.DownloadAsync(file, tmp);

            (await File.ReadAllBytesAsync(tmp)).Should().Equal(plain);
            _enc.Received(1).DecryptBytes(Arg.Any<byte[]>());
        }
        finally
        {
            File.Delete(tmp);
        }
    }

    [Fact]
    public async Task DownloadAsync_Unencrypted_WritesBytesAsIs()
    {
        var tmp = Path.GetTempFileName();
        try
        {
            var raw = new byte[] { 7, 7, 7 };
            _api.GetFileGetUrlAsync(Arg.Is("sess"), Arg.Is("file-4"), Arg.Any<CancellationToken>())
                .Returns(new PresignedUrl("https://r2.test/get", 300));
            var svc = NewService(new CapturingHandler(HttpStatusCode.OK) { ResponseBody = raw });
            var file = new HostedFile("file-4", "a", 3, null, Encrypted: false, Folder: null, CreatedAt: "t");

            await svc.DownloadAsync(file, tmp);

            (await File.ReadAllBytesAsync(tmp)).Should().Equal(raw);
            _enc.DidNotReceiveWithAnyArgs().DecryptBytes(default!);
        }
        finally
        {
            File.Delete(tmp);
        }
    }

    [Fact]
    public async Task ListAsync_ReturnsTheAccountsFiles()
    {
        _api.ListFilesAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new HostedFileList([new HostedFile("f1", "a.pdf", 10, null, Encrypted: false, Folder: "Docs", CreatedAt: "2026-06-02")]));
        var svc = NewService(new CapturingHandler(HttpStatusCode.OK));

        (await svc.ListAsync()).Should().ContainSingle(f => f.Id == "f1" && f.Folder == "Docs");
    }

    [Fact]
    public async Task DeleteAsync_DelegatesToTheApi()
    {
        var svc = NewService(new CapturingHandler(HttpStatusCode.OK));

        await svc.DeleteAsync("f1");

        await _api.Received(1).DeleteFileAsync(Arg.Is("sess"), Arg.Is("f1"), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateShareLinkAsync_ReturnsTheShareUrl()
    {
        _api.CreateShareAsync(Arg.Is("sess"), Arg.Is("f1"), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(new ShareLink("tok", "https://api.test/s/tok"));
        var svc = NewService(new CapturingHandler(HttpStatusCode.OK));

        (await svc.CreateShareLinkAsync("f1")).Should().Be("https://api.test/s/tok");
    }

    [Fact]
    public async Task RevokeShareAsync_DelegatesToTheApi()
    {
        var svc = NewService(new CapturingHandler(HttpStatusCode.OK));

        await svc.RevokeShareAsync("tok");

        await _api.Received(1).RevokeShareAsync(Arg.Is("sess"), Arg.Is("tok"), Arg.Any<CancellationToken>());
    }

    private sealed class TokenStore : IOnlineTokenStore
    {
        public OnlineTokens? Tokens;
        public OnlineTokens? Load() => Tokens;
        public void Save(OnlineTokens tokens) => Tokens = tokens;
        public void Clear() => Tokens = null;
    }

    private sealed class CapturingHandler(HttpStatusCode code) : HttpMessageHandler
    {
        public HttpRequestMessage? LastRequest { get; private set; }
        public byte[]? LastBody { get; private set; }
        public byte[]? ResponseBody { get; set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            if (request.Content is not null) LastBody = await request.Content.ReadAsByteArrayAsync(cancellationToken);
            var resp = new HttpResponseMessage(code);
            if (ResponseBody is not null) resp.Content = new ByteArrayContent(ResponseBody);
            return resp;
        }
    }
}
