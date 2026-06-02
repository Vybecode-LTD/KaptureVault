using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using FluentAssertions;
using Kapture.Models;
using Kapture.Services;
using Kapture.Services.CloudSync;
using Kapture.Services.CloudSync.Online;
using Kapture.ViewModels;
using Kapture.Views;
using NSubstitute;

namespace KaptureVault.Tests.Views;

/// <summary>
/// T-16 headless harness. Proves the main window builds and binds under the Avalonia headless
/// platform, and that the real sidebar ListBox two-way <c>SelectedItem</c> binding does not lose
/// the user's filter across a background refresh — the control-level half of the KV-013 regression
/// that <see cref="ViewModels.MainWindowViewModelFilterTests"/> covers in isolation. This is the
/// harness that makes the upcoming T-09 (Entries diff-update) and T-08 (teardown) work verifiable.
/// </summary>
public class MainWindowSmokeTests
{
    private static MainWindowViewModel BuildViewModel()
    {
        var db = Substitute.For<IDatabaseService>();
        db.GetDistinctApps().Returns(_ => new List<string> { "Chrome", "Code" });
        db.GetDistinctTags().Returns(_ => new List<string> { "work" });
        var all = new List<CaptureEntry>
        {
            new() { Id = 1, AppName = "Chrome", EntryType = "keyboard", Content = "alpha" },
            new() { Id = 2, AppName = "Code", EntryType = "keyboard", Content = "beta" },
        };
        db.GetAll(Arg.Any<int?>()).Returns(_ => all.ToList());
        db.GetByApp(Arg.Any<string>(), Arg.Any<int?>())
          .Returns(ci => all.Where(e => e.AppName == (string)ci[0]!).ToList());
        db.Search(Arg.Any<string>(), Arg.Any<string?>()).Returns(_ => all.ToList());

        // P5: the main window now binds the Online Vault account panel (Login / Sync / Web Vault /
        // Upload). Give it a real account VM over mocks so those toolbar bindings resolve under headless.
        var account = new OnlineAccountViewModel(
            Substitute.For<IOnlineAccountService>(),
            Substitute.For<IUrlOpener>(),
            new OnlineVaultConfig(),
            Substitute.For<IEncryptionService>(),
            Substitute.For<IOnlineVaultSync>());

        return new MainWindowViewModel(
            db,
            Substitute.For<ICaptureService>(),
            Substitute.For<IClipboardMonitorService>(),
            Substitute.For<IStartupService>(),
            Substitute.For<ISettingsService>(),
            Substitute.For<IScreenshotService>(),
            account);
    }

    [AvaloniaFact]
    public void MainWindow_ConstructsAndShows_Headless_WithoutThrowing()
    {
        var vm = BuildViewModel();

        var window = new MainWindow { DataContext = vm };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        window.DataContext.Should().BeSameAs(vm);
        window.IsVisible.Should().BeTrue();

        window.Close();
    }

    [AvaloniaFact]
    public void AppFilter_SurvivesBackgroundRefresh_ThroughTheRealListBoxBinding()
    {
        var vm = BuildViewModel();
        var window = new MainWindow { DataContext = vm };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        vm.SelectedAppFilter = "Chrome";
        Dispatcher.UIThread.RunJobs();

        // A background capture flush becomes a full Refresh() on the UI thread.
        vm.Refresh();
        Dispatcher.UIThread.RunJobs();

        vm.SelectedAppFilter.Should().Be("Chrome",
            "the bound sidebar ListBox must not push a deferred null back into the filter (KV-013)");

        window.Close();
    }
}
