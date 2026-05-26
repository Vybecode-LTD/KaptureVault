using Kapture.Models;

namespace Kapture.Services;

public interface IDatabaseService
{
    void Initialize();
    void Insert(CaptureEntry entry);
    List<CaptureEntry> GetAll();
    List<CaptureEntry> GetByApp(string appName);
    List<CaptureEntry> Search(string query, string? appFilter = null);
    void Delete(long id);
    void UpdatePin(long id, bool isPinned);
    void UpdateExpiry(long id, DateTime? expiresAt);
    void UpdateTags(long id, string tags);
    void PruneExpired();
    int PruneOlderThan(int days, bool excludePinned);
    List<string> GetDistinctApps();
    List<string> GetDistinctTags();
    (int totalEntries, long totalChars, int distinctApps, int clipboardEntries, int screenshotEntries) GetStats();

    /// <summary>Encrypt all existing plaintext entries. Called when encryption is first enabled.</summary>
    int EncryptAllEntries();

    /// <summary>Decrypt all existing encrypted entries. Called when encryption is disabled.</summary>
    int DecryptAllEntries();

    /// <summary>Create a safe backup copy of the database using VACUUM INTO (no file-lock conflicts).</summary>
    void CreateBackupCopy(string destinationPath);

    /// <summary>Clear the SQLite connection pool so the DB file can be replaced externally.</summary>
    void ClearConnectionPool();

    /// <summary>
    /// Safely replaces the live database from a downloaded sync file.
    /// Validates the file, blocks all other DB access during replacement,
    /// and reinitializes the schema afterward.
    /// </summary>
    Task ReplaceDatabaseFromAsync(string tempPath, CancellationToken ct);
}
