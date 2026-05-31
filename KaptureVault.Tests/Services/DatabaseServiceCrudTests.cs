using FluentAssertions;
using Kapture.Models;
using Kapture.Services;
using Microsoft.Data.Sqlite;
using Xunit;

namespace KaptureVault.Tests.Services;

/// <summary>
/// Locks the DatabaseService column mapping (KV-009 — reads now resolve columns by name
/// rather than fixed ordinals) and exercises the basic CRUD/update surface. Uses a shared
/// in-memory SQLite DB; no encryption so content round-trips verbatim.
/// </summary>
public class DatabaseServiceCrudTests : IDisposable
{
    private readonly string _connString;
    private readonly SqliteConnection _keepAlive;

    public DatabaseServiceCrudTests()
    {
        _connString = $"Data Source=file:kvtest-{Guid.NewGuid():N}?mode=memory&cache=shared";
        _keepAlive = new SqliteConnection(_connString);
        _keepAlive.Open();
    }

    private DatabaseService NewDb()
    {
        var db = new DatabaseService(null, _connString);
        db.Initialize();
        return db;
    }

    [Fact]
    public void InsertThenGetAll_RoundTripsEveryField()
    {
        var db = NewDb();
        var expires = new DateTime(2030, 1, 2, 3, 4, 5, DateTimeKind.Utc);
        var captured = new DateTime(2026, 5, 30, 10, 11, 12, DateTimeKind.Utc);

        db.Insert(new CaptureEntry
        {
            AppName = "code",
            WindowTitle = "Program.cs - KaptureVault",
            Content = "var x = 1;",
            CharCount = 10,
            CapturedAt = captured,
            ExpiresAt = expires,
            IsPinned = true,
            EntryType = "clipboard",
            DetectedLanguage = "csharp",
            Tags = "work,snippet"
        });

        var e = db.GetAll().Should().ContainSingle().Subject;
        e.Id.Should().BeGreaterThan(0);
        e.AppName.Should().Be("code");
        e.WindowTitle.Should().Be("Program.cs - KaptureVault");
        e.Content.Should().Be("var x = 1;");
        e.CharCount.Should().Be(10);
        e.CapturedAt.Should().BeCloseTo(captured, TimeSpan.FromSeconds(1));
        e.ExpiresAt.Should().NotBeNull();
        e.ExpiresAt!.Value.Should().BeCloseTo(expires, TimeSpan.FromSeconds(1));
        e.IsPinned.Should().BeTrue();
        e.EntryType.Should().Be("clipboard");
        e.DetectedLanguage.Should().Be("csharp");
        e.Tags.Should().Be("work,snippet");
    }

    [Fact]
    public void NullExpiry_RoundTripsAsNull()
    {
        var db = NewDb();
        db.Insert(new CaptureEntry
        {
            AppName = "notepad",
            WindowTitle = "t",
            Content = "hi",
            CharCount = 2,
            CapturedAt = DateTime.UtcNow,
            ExpiresAt = null,
            EntryType = "keyboard",
            Tags = ""
        });

        db.GetAll().Single().ExpiresAt.Should().BeNull();
    }

    [Fact]
    public void UpdatePinAndTags_Persist()
    {
        var db = NewDb();
        db.Insert(new CaptureEntry
        {
            AppName = "notepad",
            WindowTitle = "t",
            Content = "hi",
            CharCount = 2,
            CapturedAt = DateTime.UtcNow,
            EntryType = "keyboard",
            Tags = ""
        });
        var id = db.GetAll().Single().Id;

        db.UpdatePin(id, true);
        db.UpdateTags(id, "alpha,beta");

        var e = db.GetAll().Single();
        e.IsPinned.Should().BeTrue();
        e.Tags.Should().Be("alpha,beta");
        db.GetDistinctTags().Should().Contain(new[] { "alpha", "beta" });
    }

    [Fact]
    public void GetAll_WithLimit_ReturnsMostRecentCapped()
    {
        var db = NewDb();
        var baseTime = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        for (var i = 0; i < 5; i++)
            db.Insert(new CaptureEntry
            {
                AppName = "app",
                WindowTitle = "t",
                Content = $"entry {i}",
                CharCount = 1,
                CapturedAt = baseTime.AddMinutes(i),
                EntryType = "keyboard",
                Tags = ""
            });

        var limited = db.GetAll(limit: 2);

        limited.Should().HaveCount(2);
        // ordered by captured_at DESC → the two newest (entry 4, entry 3)
        limited.Select(e => e.Content).Should().Equal("entry 4", "entry 3");
        db.GetAll().Should().HaveCount(5); // unlimited default unchanged
    }

    public void Dispose()
    {
        _keepAlive.Dispose();
        SqliteConnection.ClearAllPools();
    }
}
