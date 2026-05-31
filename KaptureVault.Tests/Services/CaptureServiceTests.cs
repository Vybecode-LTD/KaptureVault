using System.Diagnostics;
using FluentAssertions;
using Kapture.Models;
using Kapture.Services;
using NSubstitute;
using Xunit;

namespace KaptureVault.Tests.Services;

/// <summary>
/// Coverage for the capture pipeline:
/// <list type="bullet">
/// <item>KV-005 — KaptureVault must exclude its own process so it never logs the
/// keystrokes a user types into its own UI (tag box, search box, dialogs).</item>
/// <item>KV-012 / T-07 — the SQLite INSERT must run on a writer task, never on the
/// WH_KEYBOARD_LL hook callback thread (a blocking DB write there degrades
/// system-wide input latency and risks hook eviction).</item>
/// </list>
///
/// These tests drive the real <see cref="CaptureService"/> with mocked collaborators.
/// <c>Type</c> raises the hook events synchronously on the test thread, so the test
/// thread stands in for the keyboard-hook callback thread. <c>svc.Stop()</c> drains
/// the writer queue, giving the async insert a deterministic barrier to assert after.
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

        svc.Stop();    // drains the writer queue — any queued insert would land by now
        db.DidNotReceive().Insert(Arg.Any<CaptureEntry>());
    }

    [Fact]
    public void Flush_WhenActiveWindowIsAnotherApp_CapturesEntry()
    {
        var (svc, hook, db) = CreateService(activeApp: "chrome");
        svc.Start();

        Type(hook, 3);

        svc.Stop();    // drains the writer queue
        db.Received(1).Insert(Arg.Is<CaptureEntry>(e =>
            e.AppName == "chrome" && e.Content == "aaa" && e.CharCount == 3));
    }

    [Fact]
    public void Flush_DoesNotBlockTheHookThreadOnTheDatabaseWrite()
    {
        // KV-012 / T-07: a synchronous SQLite INSERT inside the WH_KEYBOARD_LL callback
        // blocks system-wide input and risks hook eviction. Filling the buffer must hand
        // the entry to the writer queue and return immediately, even when the DB write is
        // slow. We model a slow write with a gate and assert the producer never waits on
        // it. (A thread-identity check is unreliable here: Stop().Wait() can run the
        // writer inline on the draining thread — acceptable at shutdown — so we assert the
        // property that actually matters: the hook-thread flush does not block.)
        var (svc, hook, db) = CreateService(activeApp: "chrome");
        using var release = new ManualResetEventSlim(false);
        using var insertStarted = new ManualResetEventSlim(false);
        db.When(d => d.Insert(Arg.Any<CaptureEntry>())).Do(_ =>
        {
            insertStarted.Set();
            release.Wait(TimeSpan.FromSeconds(5)); // hold the writer mid-write
        });

        svc.Start();

        var sw = Stopwatch.StartNew();
        Type(hook, 3);   // fills MaxBufferChars → flush runs on THIS (hook) thread
        sw.Stop();

        sw.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(2),
            "filling the buffer must enqueue to the writer task, never block on the SQLite write");
        insertStarted.Wait(TimeSpan.FromSeconds(2)).Should().BeTrue(
            "the writer task should pick up the queued entry and begin the (gated) write off the hook thread");

        release.Set();   // let the writer finish
        svc.Stop();      // drains the writer queue
        db.Received(1).Insert(Arg.Any<CaptureEntry>());
    }

    [Fact]
    public void Stop_DrainsBufferedEntriesAndDoesNotLoseData()
    {
        // The buffer below MaxBufferChars only flushes on Stop(); the drain must still
        // persist it (no data loss on shutdown).
        var (svc, hook, db) = CreateService(activeApp: "chrome", maxBuffer: 100);
        svc.Start();

        Type(hook, 5);   // 5 < 100 → nothing flushed yet
        db.DidNotReceive().Insert(Arg.Any<CaptureEntry>());

        svc.Stop();      // flushes the remaining buffer and drains
        db.Received(1).Insert(Arg.Is<CaptureEntry>(e => e.Content == "aaaaa" && e.CharCount == 5));
    }
}
