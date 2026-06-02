using System.Net;
using FluentAssertions;
using Kapture.Services.CloudSync.Online;
using NSubstitute;
using Xunit;

namespace KaptureVault.Tests.Services.CloudSync;

/// <summary>
/// Phase 6 client file-hosting pipeline: UploadAsync = presigned PUT-url → PUT the bytes straight to
/// R2 → commit; plus list / delete / share. Uses the REAL OnlineAccountService (a valid in-memory
/// token, so ExecuteAuthedAsync forwards the session) over a mocked api client + a stub R2 handler —
/// no network. The 250 MB cap is enforced client-side before any request.
/// </summary>
public class FileHostingServiceTests
{
    private readonly IKaptureOnlineApiClient _api = Substitute.For<IKaptureOnlineApiClient>();
    private readonly IGoogleSignIn _signIn = Substitute.For<IGoogleSignIn>();
    private readonly TokenStore _store = new()
    {
        Tokens = new OnlineTokens("sess", "refresh", DateTime.UtcNow.AddHours(1), "u-1"),
    };

    private FileHostingService NewService(out CapturingHandler r2)
    {
        r2 = new CapturingHandler(HttpStatusCode.OK);
        var account = new OnlineAccountService(_api, _signIn, _store);
        return new FileHostingService(account, _api, new HttpClient(r2));
    }

    [Fact]
    public async Task UploadAsync_RequestsPutUrl_PutsBytesToR2_ThenCommits()
    {
        var tmp = Path.GetTempFileName();
        await File.WriteAllBytesAsync(tmp, new byte[1234]);
        try
        {
            var name = Path.GetFileName(tmp);
            _api.CreateFilePutUrlAsync("sess", name, 1234, Arg.Any<string?>(), Arg.Any<CancellationToken>())
                .Returns(new FileUploadTicket("file-1", name, "https://r2.test/put?X-Amz-Signature=abc", 300));
            _api.CommitFileAsync("sess", "file-1", Arg.Any<string?>(), Arg.Any<CancellationToken>())
                .Returns(new FileCommitResult("file-1", 1234, 1234, 9_999_999));

            var svc = NewService(out var r2);
            var file = await svc.UploadAsync(tmp);

            file.Id.Should().Be("file-1");
            file.Size.Should().Be(1234);
            // The bytes went straight to the presigned R2 URL via PUT — not through the API.
            r2.LastRequest!.Method.Should().Be(HttpMethod.Put);
            r2.LastRequest.RequestUri!.AbsoluteUri.Should().Contain("r2.test/put");
            await _api.Received(1).CommitFileAsync("sess", "file-1", Arg.Any<string?>(), Arg.Any<CancellationToken>());
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
            var svc = NewService(out _);
            var act = () => svc.UploadAsync(tmp);

            await act.Should().ThrowAsync<InvalidOperationException>();
            await _api.DidNotReceiveWithAnyArgs().CreateFilePutUrlAsync(default!, default!, default, default, default);
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
            _api.CreateFilePutUrlAsync("sess", Arg.Any<string>(), Arg.Any<long>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
                .Returns(new FileUploadTicket("file-x", "a", "https://r2.test/put", 300));
            var account = new OnlineAccountService(_api, _signIn, _store);
            var svc = new FileHostingService(account, _api, new HttpClient(new CapturingHandler(HttpStatusCode.Forbidden)));

            await ((Func<Task>)(() => svc.UploadAsync(tmp))).Should().ThrowAsync<InvalidOperationException>();
            await _api.DidNotReceiveWithAnyArgs().CommitFileAsync(default!, default!, default, default);
        }
        finally
        {
            File.Delete(tmp);
        }
    }

    [Fact]
    public async Task ListAsync_ReturnsTheAccountsFiles()
    {
        _api.ListFilesAsync("sess", Arg.Any<CancellationToken>())
            .Returns(new HostedFileList([new HostedFile("f1", "a.pdf", 10, null, "2026-06-02")]));
        var svc = NewService(out _);

        (await svc.ListAsync()).Should().ContainSingle(f => f.Id == "f1" && f.Name == "a.pdf");
    }

    [Fact]
    public async Task DeleteAsync_DelegatesToTheApi()
    {
        var svc = NewService(out _);

        await svc.DeleteAsync("f1");

        await _api.Received(1).DeleteFileAsync("sess", "f1", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateShareLinkAsync_ReturnsTheShareUrl()
    {
        _api.CreateShareAsync("sess", "f1", Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(new ShareLink("tok", "https://api.test/s/tok"));
        var svc = NewService(out _);

        (await svc.CreateShareLinkAsync("f1")).Should().Be("https://api.test/s/tok");
    }

    [Fact]
    public async Task RevokeShareAsync_DelegatesToTheApi()
    {
        var svc = NewService(out _);

        await svc.RevokeShareAsync("tok");

        await _api.Received(1).RevokeShareAsync("sess", "tok", Arg.Any<CancellationToken>());
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

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            if (request.Content is not null) await request.Content.ReadAsByteArrayAsync(cancellationToken); // drain
            return new HttpResponseMessage(code);
        }
    }
}
