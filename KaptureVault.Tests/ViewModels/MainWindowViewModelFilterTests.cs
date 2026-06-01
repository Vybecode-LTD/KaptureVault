using FluentAssertions;
using Kapture.Models;
using Kapture.Services;
using Kapture.ViewModels;
using NSubstitute;
using Xunit;

namespace KaptureVault.Tests.ViewModels;

/// <summary>
/// T-16 / KV-013: regression guards for the sidebar-filter and selected-entry behaviour that the
/// v1.0.2 diff-update fix introduced. A background capture raises <c>OnEntryFlushed</c>, which the
/// view model turns into a full <see cref="MainWindowViewModel.Refresh"/> on the UI thread — that
/// must NOT silently reset the user's app/tag filter or drop a still-present selection. These run
/// the view model directly (no Avalonia ListBox), so they pin the view-model contract; the headless
/// harness (<see cref="Views.MainWindowSmokeTests"/>) covers the control-level binding behaviour.
/// They also lock the contract that T-09 (Entries diff-update) must preserve when it lands.
/// </summary>
public class MainWindowViewModelFilterTests
{
    private static MainWindowViewModel CreateVm(
        out IDatabaseService db,
        IEnumerable<string>? apps = null,
        IEnumerable<string>? tags = null,
        IEnumerable<CaptureEntry>? entries = null)
    {
        var appList = (apps ?? ["Chrome", "Code"]).ToList();
        var tagList = (tags ?? ["work"]).ToList();
        // Use the caller's list by reference when supplied, so a test can mutate the backing
        // store (e.g. delete an entry) and have the substituted DB reflect it on the next call.
        var entryList = entries as List<CaptureEntry> ?? entries?.ToList() ??
        [
            new CaptureEntry { Id = 1, AppName = "Chrome", EntryType = "keyboard", Content = "alpha", Tags = "work" },
            new CaptureEntry { Id = 2, AppName = "Code", EntryType = "keyboard", Content = "beta" },
        ];

        db = Substitute.For<IDatabaseService>();
        db.GetDistinctApps().Returns(_ => appList.ToList());
        db.GetDistinctTags().Returns(_ => tagList.ToList());
        db.GetAll(Arg.Any<int?>()).Returns(_ => entryList.ToList());
        db.GetByApp(Arg.Any<string>(), Arg.Any<int?>())
          .Returns(ci => entryList.Where(e => e.AppName == (string)ci[0]!).ToList());
        db.Search(Arg.Any<string>(), Arg.Any<string?>())
          .Returns(ci =>
          {
              var appFilter = ci[1] as string;
              return entryList.Where(e => appFilter == null || e.AppName == appFilter).ToList();
          });

        return new MainWindowViewModel(
            db,
            Substitute.For<ICaptureService>(),
            Substitute.For<IClipboardMonitorService>(),
            Substitute.For<IStartupService>(),
            Substitute.For<ISettingsService>(),
            Substitute.For<IScreenshotService>());
    }

    [Fact]
    public void Construction_PopulatesAppAndTagLists_WithTheAllSentinelFirst()
    {
        var vm = CreateVm(out _);

        vm.AppList.Should().Equal("All Apps", "Chrome", "Code");
        vm.TagList.Should().Equal("All Tags", "work");
        vm.Entries.Should().HaveCount(2);
    }

    [Fact]
    public void SelectedAppFilter_SurvivesABackgroundRefresh()
    {
        var vm = CreateVm(out _);

        vm.SelectedAppFilter = "Chrome";
        vm.Refresh(); // simulates OnEntryFlushed -> Dispatcher.Post(Refresh) from a background capture

        vm.SelectedAppFilter.Should().Be("Chrome",
            "a background refresh must not reset the user's app filter (KV-013 / the v1.0.2 regression)");
    }

    [Fact]
    public void SelectedTagFilter_SurvivesABackgroundRefresh()
    {
        var vm = CreateVm(out _);

        vm.SelectedTagFilter = "work";
        vm.Refresh();

        vm.SelectedTagFilter.Should().Be("work",
            "a background refresh must not reset the user's tag filter");
    }

    [Fact]
    public void SelectingAppFilter_NarrowsEntriesToThatApp()
    {
        var vm = CreateVm(out var db);

        vm.SelectedAppFilter = "Chrome";

        vm.Entries.Should().OnlyContain(e => e.AppName == "Chrome");
        db.Received().GetByApp("Chrome", Arg.Any<int?>());
    }

    [Fact]
    public void SelectedEntry_SurvivesARefresh_WhenStillPresent()
    {
        var vm = CreateVm(out _);
        vm.SelectedEntry = vm.Entries.Single(e => e.Id == 1);

        vm.Refresh();

        vm.SelectedEntry.Should().NotBeNull();
        vm.SelectedEntry!.Id.Should().Be(1);
    }

    [Fact]
    public void SelectedEntry_BecomesNull_WhenItLeavesTheVault()
    {
        var entries = new List<CaptureEntry>
        {
            new() { Id = 1, AppName = "Chrome", EntryType = "keyboard", Content = "alpha" },
            new() { Id = 2, AppName = "Code", EntryType = "keyboard", Content = "beta" },
        };
        var vm = CreateVm(out var db, entries: entries);
        vm.SelectedEntry = vm.Entries.Single(e => e.Id == 1);

        // Entry 1 is deleted out from under the selection, then a refresh occurs.
        entries.RemoveAll(e => e.Id == 1);
        vm.Refresh();

        vm.SelectedEntry.Should().BeNull("a selection that has left the vault should clear, not dangle");
        vm.Entries.Select(e => e.Id).Should().Equal(2);
    }

    [Fact]
    public void TypeFilter_Screenshot_ShowsOnlyScreenshots()
    {
        var entries = new List<CaptureEntry>
        {
            new() { Id = 1, AppName = "Chrome", EntryType = "keyboard", Content = "typed" },
            new() { Id = 2, AppName = "Chrome", EntryType = "screenshot", Content = @"C:\shot.bmp" },
        };
        var vm = CreateVm(out _, entries: entries);

        vm.SelectedTypeFilter = "Screenshot";

        vm.Entries.Should().OnlyContain(e => e.EntryType == "screenshot");
    }
}
