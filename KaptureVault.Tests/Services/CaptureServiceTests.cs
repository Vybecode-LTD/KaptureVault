using System.Diagnostics;
using FluentAssertions;
using Kapture.Models;
using Kapture.Services;
using NSubstitute;
using Xunit;

namespace KaptureVault.Tests.Services;

/// <summary>
/// Regression coverage for KV-005 — the capture pipeline must exclude KaptureVault's
/// own process so it never logs the keystrokes a user types into its own UI
/// (tag box, search box, dialogs).
///
/// The bug: <c>SelfProcessName</c> was the literal "Kapture", but the renamed
/// process is "KaptureVault", so the self-check never matched. These tests drive
/// the real <see cref="CaptureService"/> with mocked collaborators and use the
/// CURRENT test process's own name as the "active window" — the self-exclusion
/// must hold for whatever the running process is actually called.
/// </summary>
public class CaptureServiceTests
{
    private static readonly string SelfName = Process.GetCurrentProcess().ProcessName;

    private static (CaptureService svc, IKeyboardHookService hook, IDatabaseService db)
        CreateService(string activeApp, int maxBuffer = 3)
    {
        var hook = Substitute.For<IKeyboardHookService>();
        var window = Substitute.For<IActiveWindowService>();
        var db = Substitute.For<IDatabaseService>();
        var settings = Substitute.For<ISettingsService>();
        settings.Settings.Returns(new AppSettings { MaxBufferChars = maxBuffer, IdleFlushSeconds = 999 });
        window.GetActiveWindow().Returns(new ActiveWindowInfo(activeApp, "title"));

        var svc = new CaptureService(hook, window, db, settings);
        return (svc, hook, db);
    }

    private static void Type(IKeyboardHookService hook, int count)
    {
        for (var i = 0; i < count; i++)
            hook.OnCharTyped += Raise.Event<Action<char>>('a');
    }

    [Fact]
    public void Flush_WhenActiveWindowIsKaptureVaultItself_DoesNotCapture()
    {
        // Active window belongs to *this* process → must be treated as self and skipped.
        var (svc, hook, db) = CreateService(activeApp: SelfName);
        svc.Start();

        Type(hook, 3); // reaches MaxBufferChars → triggers a flush

        db.DidNotReceive().Insert(Arg.Any<CaptureEntry>());
        svc.Stop();
    }

    [Fact]
    public void Flush_WhenActiveWindowIsAnotherApp_CapturesEntry()
    {
        var (svc, hook, db) = CreateService(activeApp: "chrome");
        svc.Start();

        Type(hook, 3);

        db.Received(1).Insert(Arg.Is<CaptureEntry>(e =>
            e.AppName == "chrome" && e.Content == "aaa" && e.CharCount == 3));
        svc.Stop();
    }
}
