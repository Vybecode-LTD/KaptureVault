using FluentAssertions;
using Kapture.Models;
using Kapture.Services;
using Microsoft.Data.Sqlite;
using Xunit;

namespace KaptureVault.Tests.Services;

/// <summary>
/// F-01 (Export Vault Database): the "Export Vault Database…" command writes a backup via
/// <see cref="DatabaseService.CreateBackupCopy"/> (VACUUM INTO). These lock the export
/// contract — the produced file is a complete, standalone SQLite database containing every
/// row — so a user can restore from it. The primitive already existed (Drive sync uses it);
/// this surfaces and characterizes it for the new user-facing export.
/// </summary>
public class DatabaseServiceBackupTests : IDisposable
{
    private readonly string _connString;
    private readonly SqliteConnection _keepAlive;
    private readonly string _backupPath;

    public DatabaseServiceBackupTests()
    {
        _connString = $"Data Source=file:kvbackup-{Guid.NewGuid():N}?mode=memory&cache=shared";
        _keepAlive = new SqliteConnection(_connString);
        _keepAlive.Open();
        _backupPath = Path.Combine(Path.GetTempPath(), $"kvbackup-{Guid.NewGuid():N}.db");
    }

    private DatabaseService NewDb()
    {
        var db = new DatabaseService(null, _connString);
        db.Initialize();
        return db;
    }

    [Fact]
    public void CreateBackupCopy_WritesStandaloneFileWithEveryRow()
    {
        var db = NewDb();
        for (var i = 0; i < 3; i++)
            db.Insert(new CaptureEntry
            {
                AppName = "code",
                WindowTitle = "t",
                Content = $"entry {i}",
                CharCount = 7,
                CapturedAt = DateTime.UtcNow,
                EntryType = "keyboard",
                Tags = ""
            });

        db.CreateBackupCopy(_backupPath);

        File.Exists(_backupPath).Should().BeTrue("the export must produce a file at the chosen path");
        new FileInfo(_backupPath).Length.Should().BeGreaterThan(0);

        // Open the backup as an INDEPENDENT database (its own file connection, no shared
        // cache) — i.e. exactly what a restore would do — and confirm every row survived.
        var restored = new DatabaseService(null, $"Data Source={_backupPath}");
        restored.Initialize(); // CREATE TABLE IF NOT EXISTS — idempotent on the populated copy
        var rows = restored.GetAll();
        rows.Should().HaveCount(3);
        rows.Select(e => e.Content).Should().BeEquivalentTo("entry 0", "entry 1", "entry 2");
    }

    [Fact]
    public void CreateBackupCopy_OnEmptyVault_StillProducesAValidDatabase()
    {
        var db = NewDb();

        db.CreateBackupCopy(_backupPath);

        File.Exists(_backupPath).Should().BeTrue();
        var restored = new DatabaseService(null, $"Data Source={_backupPath}");
        restored.Initialize();
        restored.GetAll().Should().BeEmpty();
    }

    public void Dispose()
    {
        _keepAlive.Dispose();
        SqliteConnection.ClearAllPools();
        if (File.Exists(_backupPath))
        {
            try { File.Delete(_backupPath); }
            catch { /* best-effort temp cleanup */ }
        }
    }
}
