using FluentAssertions;
using Kapture.Models;
using Kapture.Services;
using Kapture.ViewModels;
using NSubstitute;
using Xunit;

namespace KaptureVault.Tests.ViewModels;

/// <summary>
/// T-09 / KV-013: the entry list is diff-updated in place (instances reused by Id, reordered,
/// trimmed) instead of Clear()+rebuilt — so the two-way bound SelectedItem is never dropped. The
/// fake DB returns a FRESH CaptureEntry per row on every query (as the real DatabaseService does),
/// which is what makes "did we reuse or rebuild?" observable. Also pins CaptureEntry's new change
/// notifications, which let an in-place edit repaint without a rebuild.
/// </summary>
public class MainWindowViewModelEntriesDiffTests
{
    private sealed record Row(long Id, string App, string Type = "keyboard");

    private static MainWindowViewModel CreateVm(List<Row> store, out IDatabaseService db)
    {
        static CaptureEntry Make(Row r) =>
            new() { Id = r.Id, AppName = r.App, EntryType = r.Type, Content = $"c{r.Id}" };

        db = Substitute.For<IDatabaseService>();
        db.GetDistinctApps().Returns(_ => store.Select(r => r.App).Distinct().ToList());
        db.GetDistinctTags().Returns(_ => new List<string>());
        db.GetAll(Arg.Any<int?>()).Returns(_ => store.Select(Make).ToList());
        db.GetByApp(Arg.Any<string>(), Arg.Any<int?>())
          .Returns(ci => store.Where(r => r.App == (string)ci[0]!).Select(Make).ToList());
        db.Search(Arg.Any<string>(), Arg.Any<string?>()).Returns(_ => store.Select(Make).ToList());

        return new MainWindowViewModel(
            db,
            Substitute.For<ICaptureService>(),
            Substitute.For<IClipboardMonitorService>(),
            Substitute.For<IStartupService>(),
            Substitute.For<ISettingsService>(),
            Substitute.For<IScreenshotService>());
    }

    [Fact]
    public void Refresh_ReusesExistingInstances_RatherThanRebuilding()
    {
        var store = new List<Row> { new(1, "Chrome"), new(2, "Code") };
        var vm = CreateVm(store, out _);
        var firstBefore = vm.Entries[0];
        var secondBefore = vm.Entries[1];

        vm.Refresh(); // the DB hands back brand-new instances...

        vm.Entries[0].Should().BeSameAs(firstBefore, "a diff-update keeps the existing instance for an unchanged Id");
        vm.Entries[1].Should().BeSameAs(secondBefore);
    }

    [Fact]
    public void Refresh_WithANewMostRecentEntry_InsertsItAtTop_AndKeepsExistingInstances()
    {
        var store = new List<Row> { new(1, "Chrome"), new(2, "Code") };
        var vm = CreateVm(store, out _);
        var oldFirst = vm.Entries[0];
        var oldSecond = vm.Entries[1];

        store.Insert(0, new Row(3, "Slack")); // a newer capture lands at the top
        vm.Refresh();

        vm.Entries.Select(e => e.Id).Should().Equal(3, 1, 2);
        vm.Entries[1].Should().BeSameAs(oldFirst, "existing rows are reused, not recreated");
        vm.Entries[2].Should().BeSameAs(oldSecond);
    }

    [Fact]
    public void Refresh_RemovesAGoneEntry_AndKeepsTheRestInPlace()
    {
        var store = new List<Row> { new(1, "Chrome"), new(2, "Code"), new(3, "Slack") };
        var vm = CreateVm(store, out _);
        var keep1 = vm.Entries[0];
        var keep3 = vm.Entries[2];

        store.RemoveAll(r => r.Id == 2);
        vm.Refresh();

        vm.Entries.Select(e => e.Id).Should().Equal(1, 3);
        vm.Entries[0].Should().BeSameAs(keep1);
        vm.Entries[1].Should().BeSameAs(keep3);
    }

    [Fact]
    public void CaptureEntry_IsPinned_RaisesPropertyChanged()
    {
        var entry = new CaptureEntry { Id = 1 };
        using var monitored = entry.Monitor();

        entry.IsPinned = true;

        monitored.Should().RaisePropertyChangeFor(e => e.IsPinned);
    }

    [Fact]
    public void CaptureEntry_Tags_RaisesPropertyChanged_ForTagsAndTagList()
    {
        var entry = new CaptureEntry { Id = 1 };
        using var monitored = entry.Monitor();

        entry.Tags = "work, urgent";

        monitored.Should().RaisePropertyChangeFor(e => e.Tags);
        monitored.Should().RaisePropertyChangeFor(e => e.TagList);
        entry.TagList.Should().Equal("work", "urgent");
    }
}
