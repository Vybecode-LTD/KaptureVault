using FluentAssertions;
using Kapture.Services.CloudSync.Online;
using Kapture.ViewModels;
using NSubstitute;
using Xunit;

namespace KaptureVault.Tests.ViewModels;

/// <summary>
/// The Files manager view model (F-02 Phase 6D): groups hosted files into virtual folders, navigates
/// them, assigns the current folder on upload, and blocks sharing a private (encrypted) file. The
/// picker/clipboard live in the window; this is the pure logic over a mocked IFileHostingService.
/// </summary>
public class FileHostingViewModelTests
{
    private readonly IFileHostingService _files = Substitute.For<IFileHostingService>();

    private FileHostingViewModel NewVm() => new(_files);

    private static HostedFile File(string id, string name, string? folder, bool enc = false) =>
        new(id, name, 10, null, Encrypted: enc, Folder: folder, CreatedAt: id);

    [Fact]
    public async Task Refresh_ShowsOnlyRootFiles_AndDerivesTopFolders()
    {
        _files.ListAsync(Arg.Any<CancellationToken>()).Returns(new List<HostedFile>
        {
            File("1", "root.txt", null),
            File("2", "a.txt", "Docs"),
            File("3", "b.txt", "Docs/Sub"),
            File("4", "c.txt", "Photos"),
        });
        var vm = NewVm();

        await vm.RefreshAsync();

        vm.Files.Select(f => f.Name).Should().Equal("root.txt");
        vm.Subfolders.Should().BeEquivalentTo(new[] { "Docs", "Photos" });
    }

    [Fact]
    public async Task OpenFolder_NavigatesIn_AndGoUpReturns()
    {
        _files.ListAsync(Arg.Any<CancellationToken>()).Returns(new List<HostedFile>
        {
            File("2", "a.txt", "Docs"),
            File("3", "b.txt", "Docs/Sub"),
        });
        var vm = NewVm();
        await vm.RefreshAsync();

        vm.OpenFolder("Docs");
        vm.CurrentFolder.Should().Be("Docs");
        vm.AtRoot.Should().BeFalse();
        vm.Files.Select(f => f.Name).Should().Equal("a.txt");
        vm.Subfolders.Should().Equal("Sub");

        vm.GoUpCommand.Execute(null);
        vm.CurrentFolder.Should().Be("");
        vm.AtRoot.Should().BeTrue();
    }

    [Fact]
    public async Task Upload_AssignsTheCurrentFolder()
    {
        _files.UploadAsync("/tmp/x.bin", false, "Docs", Arg.Any<CancellationToken>()).Returns(File("9", "x.bin", "Docs"));
        var vm = NewVm();
        vm.CreateFolder("Docs"); // navigates into the new folder

        await vm.UploadAsync("/tmp/x.bin", encrypt: false);

        await _files.Received(1).UploadAsync("/tmp/x.bin", false, "Docs", Arg.Any<CancellationToken>());
        vm.Files.Select(f => f.Name).Should().Contain("x.bin");
    }

    [Fact]
    public async Task Upload_AtRoot_PassesNullFolder()
    {
        _files.UploadAsync("/tmp/y.bin", false, null, Arg.Any<CancellationToken>()).Returns(File("9", "y.bin", null));
        var vm = NewVm();

        await vm.UploadAsync("/tmp/y.bin", encrypt: false);

        await _files.Received(1).UploadAsync("/tmp/y.bin", false, null, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateFolder_NavigatesIntoIt_AndShowsItUnderTheParent()
    {
        var vm = NewVm();

        vm.CreateFolder("Reports");

        vm.CurrentFolder.Should().Be("Reports");
        vm.GoHomeCommand.Execute(null);
        vm.Subfolders.Should().Contain("Reports"); // the empty session folder is visible until reload
    }

    [Fact]
    public async Task Share_OfEncryptedFile_IsBlocked_AndDoesNotCallTheService()
    {
        var vm = NewVm();

        var url = await vm.ShareAsync(File("1", "s.txt", null, enc: true));

        url.Should().BeNull();
        await _files.DidNotReceiveWithAnyArgs().CreateShareLinkAsync(default!, default);
        vm.StatusMessage.Should().Contain("can't be shared");
    }

    [Fact]
    public async Task Share_OfShareableFile_ReturnsTheUrl()
    {
        _files.CreateShareLinkAsync("1", Arg.Any<CancellationToken>()).Returns("https://w/s/tok");
        var vm = NewVm();

        (await vm.ShareAsync(File("1", "s.txt", null))).Should().Be("https://w/s/tok");
    }

    [Fact]
    public async Task Delete_RemovesTheFileFromTheView()
    {
        _files.ListAsync(Arg.Any<CancellationToken>()).Returns(new List<HostedFile> { File("1", "a.txt", null) });
        var vm = NewVm();
        await vm.RefreshAsync();

        await vm.DeleteAsync(vm.Files[0]);

        await _files.Received(1).DeleteAsync("1", Arg.Any<CancellationToken>());
        vm.Files.Should().BeEmpty();
    }
}
