using FluentAssertions;
using Kapture.Models;
using Kapture.Services;
using Microsoft.Data.Sqlite;
using Xunit;

namespace KaptureVault.Tests.Services;

/// <summary>
/// KV-003 (mitigation): cloud sync replaces the whole local vault when the remote is
/// newer, which can clobber local-only entries. The full fix is a per-entry merge
/// (deferred), but at minimum a clobbering sync-down must leave a recovery point.
/// Previously the pre-sync backup was deleted on success — these tests pin that it is
/// now retained and contains the pre-replace local data.
/// </summary>
public class DatabaseServiceReplaceTests : IDisposable
{
    private readonly string _dir =
        Path.Combine(Path.GetTempPath(), "kvtest-" + Guid.NewGuid().ToString("N"));

    public DatabaseServiceReplaceTests() => Directory.CreateDirectory(_dir);

    private static DatabaseService FileDb(string path)
    {
        var db = new DatabaseService(null, $"Data Source={path}");
        db.Initialize();
        return db;
    }

    private static CaptureEntry Entry(string content) => new()
    {
        AppName = "app",
        WindowTitle = "t",
        Content = content,
        CharCount = content.Length,
        CapturedAt = DateTime.UtcNow,
        EntryType = "keyboard",
        Tags = ""
    };

    // Flush WAL into the main .db file so a file copy captures the rows deterministically.
    private static void Checkpoint(string path)
    {
        using var c = new SqliteConnection($"Data Source={path}");
        c.Open();
        using var cmd = c.CreateCommand();
        cmd.CommandText = "PRAGMA wal_checkpoint(TRUNCATE)";
        cmd.ExecuteNonQuery();
    }

    [Fact]
    public async Task ReplaceDatabaseFromAsync_RetainsPreSyncBackupWithLocalData()
    {
        var livePath = Path.Combine(_dir, "vault.db");
        var live = FileDb(livePath);
        live.Insert(Entry("local-only data"));

        var remotePath = Path.Combine(_dir, "remote.db");
        var remote = FileDb(remotePath);
        remote.Insert(Entry("remote data"));

        SqliteConnection.ClearAllPools();
        Checkpoint(livePath);
        Checkpoint(remotePath);
        SqliteConnection.ClearAllPools();

        await live.ReplaceDatabaseFromAsync(remotePath, CancellationToken.None);

        var backupPath = livePath + ".pre_sync_backup";
        File.Exists(backupPath).Should().BeTrue("a clobbering sync-down must leave a recovery point");

        var backup = new DatabaseService(null, $"Data Source={backupPath}");
        backup.GetAll().Should().Contain(e => e.Content == "local-only data");
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        try { if (Directory.Exists(_dir)) Directory.Delete(_dir, true); } catch { }
    }
}
