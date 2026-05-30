using FluentAssertions;
using Kapture.Models;
using Kapture.Services;
using Microsoft.Data.Sqlite;
using Xunit;

namespace KaptureVault.Tests.Services;

/// <summary>
/// KV-004: when encryption is active the <c>content</c> column holds ciphertext, so a
/// SQL <c>LIKE</c> on it can never match real text — content search silently returned
/// nothing. Search must instead match against the DECRYPTED content for encrypted vaults.
///
/// Uses a shared in-memory SQLite DB (kept alive by a held-open connection) and a real
/// EncryptionService pointed at a temp dir, so nothing touches the user's real vault.
/// </summary>
public class DatabaseServiceSearchTests : IDisposable
{
    private readonly string _tempDir =
        Path.Combine(Path.GetTempPath(), "kvtest-" + Guid.NewGuid().ToString("N"));
    private readonly string _connString;
    private readonly SqliteConnection _keepAlive;

    public DatabaseServiceSearchTests()
    {
        var name = "kvtest-" + Guid.NewGuid().ToString("N");
        _connString = $"Data Source=file:{name}?mode=memory&cache=shared";
        _keepAlive = new SqliteConnection(_connString);
        _keepAlive.Open(); // hold the shared in-memory DB alive for the whole test
    }

    private DatabaseService CreateEncryptedDb()
    {
        var enc = new EncryptionService(_tempDir);
        enc.Configure("pw");
        var db = new DatabaseService(enc, _connString);
        db.Initialize();
        return db;
    }

    private static CaptureEntry Entry(string app, string content) => new()
    {
        AppName = app,
        WindowTitle = "title",
        Content = content,
        CharCount = content.Length,
        CapturedAt = DateTime.UtcNow,
        EntryType = "keyboard",
        Tags = ""
    };

    [Fact]
    public void Search_WithEncryptionActive_FindsByDecryptedContent()
    {
        var db = CreateEncryptedDb();
        db.Insert(Entry("notepad", "the quick brown fox"));

        var results = db.Search("brown");

        results.Should().ContainSingle();
        results[0].Content.Should().Be("the quick brown fox");
    }

    [Fact]
    public void Search_ContentReallyEncryptedAtRest()
    {
        // Sanity guard: confirms the content column is ciphertext, so the test above
        // is genuinely exercising the encrypted-search path.
        var db = CreateEncryptedDb();
        db.Insert(Entry("notepad", "sekret data"));

        using var cmd = _keepAlive.CreateCommand();
        cmd.CommandText = "SELECT content FROM entries LIMIT 1";
        var raw = (string)cmd.ExecuteScalar()!;

        raw.Should().StartWith("ENC:");
        raw.Should().NotContain("sekret");
    }

    [Fact]
    public void Search_WithEncryptionActive_NoMatch_ReturnsEmpty()
    {
        var db = CreateEncryptedDb();
        db.Insert(Entry("notepad", "hello world"));

        db.Search("zzz-not-present").Should().BeEmpty();
    }

    public void Dispose()
    {
        _keepAlive.Dispose();
        SqliteConnection.ClearAllPools();
        try { if (Directory.Exists(_tempDir)) Directory.Delete(_tempDir, true); } catch { }
    }
}
