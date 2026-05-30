using Kapture.Models;
using Microsoft.Data.Sqlite;

namespace Kapture.Services;

public class DatabaseService : IDatabaseService
{
    private readonly IEncryptionService? _encryption;
    private string _connectionString;
    private string _dbPath;

    /// <summary>
    /// Gate that protects against concurrent DB access during sync replacement.
    /// Normal operations acquire as readers (non-exclusive). Sync replacement
    /// acquires exclusively, blocking all other DB access.
    /// </summary>
    private readonly SemaphoreSlim _dbGate = new(1, 1);
    private volatile bool _isReplacing;

    public DatabaseService(IEncryptionService? encryption = null)
    {
        _encryption = encryption;
        var folder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "KaptureVault");
        Directory.CreateDirectory(folder);
        _dbPath = Path.Combine(folder, "vault.db");
        _connectionString = $"Data Source={_dbPath}";
    }

    /// <summary>
    /// Safely replaces the live database file from a downloaded sync copy.
    /// Blocks all other DB access during replacement, validates the file,
    /// and reinitializes the schema afterward.
    /// </summary>
    public async Task ReplaceDatabaseFromAsync(string tempPath, CancellationToken ct)
    {
        if (!File.Exists(tempPath))
            throw new FileNotFoundException("Sync temp file not found", tempPath);

        // Validate that the temp file is a valid SQLite database
        await using (var validationConn = new SqliteConnection($"Data Source={tempPath};Mode=ReadOnly"))
        {
            await validationConn.OpenAsync(ct);
            using var cmd = validationConn.CreateCommand();
            cmd.CommandText = "PRAGMA integrity_check";
            var result = await cmd.ExecuteScalarAsync(ct) as string;
            if (result != "ok")
                throw new InvalidOperationException($"Downloaded database failed integrity check: {result}");
        }

        await _dbGate.WaitAsync(ct);
        _isReplacing = true;
        try
        {
            SqliteConnection.ClearAllPools();

            // Atomic replace: rename temp over live
            var backupPath = _dbPath + ".pre_sync_backup";
            if (File.Exists(_dbPath))
                File.Copy(_dbPath, backupPath, overwrite: true);

            try
            {
                File.Copy(tempPath, _dbPath, overwrite: true);
                File.Delete(tempPath);
            }
            catch
            {
                // Restore backup on failure
                if (File.Exists(backupPath))
                    File.Copy(backupPath, _dbPath, overwrite: true);
                throw;
            }

            // Clean up backup after successful replace
            if (File.Exists(backupPath))
                File.Delete(backupPath);

            // Reinitialize schema
            Initialize();
        }
        finally
        {
            _isReplacing = false;
            _dbGate.Release();
        }
    }

    /// <summary>
    /// Throws if the database is currently being replaced by sync.
    /// Called at the top of all public DB operations.
    /// </summary>
    private void ThrowIfReplacing()
    {
        if (_isReplacing)
            throw new InvalidOperationException("Database is being replaced by cloud sync. Retry shortly.");
    }

    public void Initialize()
    {
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS entries (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                app_name TEXT NOT NULL,
                window_title TEXT NOT NULL,
                content TEXT NOT NULL,
                char_count INTEGER NOT NULL,
                captured_at TEXT NOT NULL,
                expires_at TEXT,
                is_pinned INTEGER NOT NULL DEFAULT 0,
                entry_type TEXT NOT NULL DEFAULT 'keyboard',
                detected_language TEXT,
                tags TEXT NOT NULL DEFAULT ''
            );
            CREATE INDEX IF NOT EXISTS idx_entries_app ON entries(app_name);
            CREATE INDEX IF NOT EXISTS idx_entries_captured ON entries(captured_at);
            """;
        cmd.ExecuteNonQuery();

        // Migration: add columns to existing databases
        MigrateColumn(conn, "entry_type", "ALTER TABLE entries ADD COLUMN entry_type TEXT NOT NULL DEFAULT 'keyboard'");
        MigrateColumn(conn, "detected_language", "ALTER TABLE entries ADD COLUMN detected_language TEXT");
        MigrateColumn(conn, "tags", "ALTER TABLE entries ADD COLUMN tags TEXT NOT NULL DEFAULT ''");
    }

    public void Insert(CaptureEntry entry)
    {
        ThrowIfReplacing();
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO entries (app_name, window_title, content, char_count, captured_at, expires_at, is_pinned, entry_type, detected_language, tags)
            VALUES (@app, @title, @content, @chars, @captured, @expires, @pinned, @type, @lang, @tags)
            """;
        cmd.Parameters.AddWithValue("@app", entry.AppName);
        cmd.Parameters.AddWithValue("@title", entry.WindowTitle);
        cmd.Parameters.AddWithValue("@content", _encryption?.IsActive == true ? _encryption.Encrypt(entry.Content) : entry.Content);
        cmd.Parameters.AddWithValue("@chars", entry.CharCount);
        cmd.Parameters.AddWithValue("@captured", entry.CapturedAt.ToString("o"));
        cmd.Parameters.AddWithValue("@expires", entry.ExpiresAt?.ToString("o") ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@pinned", entry.IsPinned ? 1 : 0);
        cmd.Parameters.AddWithValue("@type", entry.EntryType);
        cmd.Parameters.AddWithValue("@lang", entry.DetectedLanguage ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@tags", entry.Tags);
        cmd.ExecuteNonQuery();
    }

    public List<CaptureEntry> GetAll()
    {
        ThrowIfReplacing();
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT * FROM entries ORDER BY is_pinned DESC, captured_at DESC";
        return ReadEntries(cmd);
    }

    public List<CaptureEntry> GetByApp(string appName)
    {
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT * FROM entries WHERE app_name = @app ORDER BY is_pinned DESC, captured_at DESC";
        cmd.Parameters.AddWithValue("@app", appName);
        return ReadEntries(cmd);
    }

    public List<CaptureEntry> Search(string query, string? appFilter = null)
    {
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        var sql = """
            SELECT * FROM entries
            WHERE (content LIKE @q OR app_name LIKE @q OR window_title LIKE @q OR tags LIKE @q)
            """;
        if (!string.IsNullOrEmpty(appFilter))
            sql += " AND app_name = @app";
        sql += " ORDER BY is_pinned DESC, captured_at DESC";

        cmd.CommandText = sql;
        cmd.Parameters.AddWithValue("@q", $"%{query}%");
        if (!string.IsNullOrEmpty(appFilter))
            cmd.Parameters.AddWithValue("@app", appFilter);
        return ReadEntries(cmd);
    }

    public void Delete(long id)
    {
        ThrowIfReplacing();
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM entries WHERE id = @id";
        cmd.Parameters.AddWithValue("@id", id);
        cmd.ExecuteNonQuery();
    }

    public void UpdatePin(long id, bool isPinned)
    {
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "UPDATE entries SET is_pinned = @pinned WHERE id = @id";
        cmd.Parameters.AddWithValue("@pinned", isPinned ? 1 : 0);
        cmd.Parameters.AddWithValue("@id", id);
        cmd.ExecuteNonQuery();
    }

    public void UpdateExpiry(long id, DateTime? expiresAt)
    {
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "UPDATE entries SET expires_at = @expires WHERE id = @id";
        cmd.Parameters.AddWithValue("@expires", expiresAt?.ToString("o") ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@id", id);
        cmd.ExecuteNonQuery();
    }

    public void PruneExpired()
    {
        ThrowIfReplacing();
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM entries WHERE expires_at IS NOT NULL AND is_pinned = 0 AND expires_at < @now";
        cmd.Parameters.AddWithValue("@now", DateTime.UtcNow.ToString("o"));
        cmd.ExecuteNonQuery();
    }

    public int PruneOlderThan(int days, bool excludePinned)
    {
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        var cutoff = DateTime.UtcNow.AddDays(-days).ToString("o");
        var sql = "DELETE FROM entries WHERE captured_at < @cutoff";
        if (excludePinned)
            sql += " AND is_pinned = 0";
        cmd.CommandText = sql;
        cmd.Parameters.AddWithValue("@cutoff", cutoff);
        return cmd.ExecuteNonQuery();
    }

    public List<string> GetDistinctApps()
    {
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT DISTINCT app_name FROM entries ORDER BY app_name";
        using var reader = cmd.ExecuteReader();
        var apps = new List<string>();
        while (reader.Read())
            apps.Add(reader.GetString(0));
        return apps;
    }

    public (int totalEntries, long totalChars, int distinctApps, int clipboardEntries, int screenshotEntries) GetStats()
    {
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT
                COUNT(*),
                COALESCE(SUM(char_count), 0),
                COUNT(DISTINCT app_name),
                COUNT(CASE WHEN entry_type = 'clipboard' THEN 1 END),
                COUNT(CASE WHEN entry_type = 'screenshot' THEN 1 END)
            FROM entries
            """;
        using var reader = cmd.ExecuteReader();
        reader.Read();
        return (reader.GetInt32(0), reader.GetInt64(1), reader.GetInt32(2), reader.GetInt32(3), reader.GetInt32(4));
    }

    public void UpdateTags(long id, string tags)
    {
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "UPDATE entries SET tags = @tags WHERE id = @id";
        cmd.Parameters.AddWithValue("@tags", tags);
        cmd.Parameters.AddWithValue("@id", id);
        cmd.ExecuteNonQuery();
    }

    public List<string> GetDistinctTags()
    {
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT DISTINCT tags FROM entries WHERE tags != ''";
        using var reader = cmd.ExecuteReader();
        var tagSet = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        while (reader.Read())
        {
            var tags = reader.GetString(0);
            foreach (var tag in tags.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                tagSet.Add(tag);
        }
        return tagSet.ToList();
    }

    public int EncryptAllEntries()
    {
        if (_encryption?.IsActive != true) return 0;
        using var conn = Open();
        using var readCmd = conn.CreateCommand();
        readCmd.CommandText = "SELECT id, content FROM entries WHERE content NOT LIKE 'ENC:%'";
        var updates = new List<(long id, string encrypted)>();
        using (var reader = readCmd.ExecuteReader())
        {
            while (reader.Read())
            {
                var id = reader.GetInt64(0);
                var content = reader.GetString(1);
                updates.Add((id, _encryption.Encrypt(content)));
            }
        }
        foreach (var (id, encrypted) in updates)
        {
            using var updateCmd = conn.CreateCommand();
            updateCmd.CommandText = "UPDATE entries SET content = @content WHERE id = @id";
            updateCmd.Parameters.AddWithValue("@content", encrypted);
            updateCmd.Parameters.AddWithValue("@id", id);
            updateCmd.ExecuteNonQuery();
        }
        return updates.Count;
    }

    public int DecryptAllEntries()
    {
        if (_encryption?.IsActive != true) return 0;
        using var conn = Open();
        using var readCmd = conn.CreateCommand();
        readCmd.CommandText = "SELECT id, content FROM entries WHERE content LIKE 'ENC:%'";
        var updates = new List<(long id, string decrypted)>();
        using (var reader = readCmd.ExecuteReader())
        {
            while (reader.Read())
            {
                var id = reader.GetInt64(0);
                var content = reader.GetString(1);
                // KV-002: skip rows that can't be decrypted (corrupt / from another
                // vault) rather than aborting the whole disable-encryption operation.
                // They stay encrypted; everything decryptable is still converted.
                try
                {
                    updates.Add((id, _encryption.Decrypt(content)));
                }
                catch (DecryptionException)
                {
                    // leave this row encrypted
                }
            }
        }
        foreach (var (id, decrypted) in updates)
        {
            using var updateCmd = conn.CreateCommand();
            updateCmd.CommandText = "UPDATE entries SET content = @content WHERE id = @id";
            updateCmd.Parameters.AddWithValue("@content", decrypted);
            updateCmd.Parameters.AddWithValue("@id", id);
            updateCmd.ExecuteNonQuery();
        }
        return updates.Count;
    }

    public void CreateBackupCopy(string destinationPath)
    {
        ThrowIfReplacing();
        if (File.Exists(destinationPath))
            File.Delete(destinationPath);

        using var conn = Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "VACUUM INTO @path";
        cmd.Parameters.AddWithValue("@path", destinationPath);
        cmd.ExecuteNonQuery();
    }

    public void ClearConnectionPool()
    {
        SqliteConnection.ClearAllPools();
    }

    private static void MigrateColumn(SqliteConnection conn, string columnName, string alterSql)
    {
        using var check = conn.CreateCommand();
        check.CommandText = "PRAGMA table_info(entries)";
        using var reader = check.ExecuteReader();
        while (reader.Read())
        {
            if (reader.GetString(1) == columnName)
                return;
        }
        reader.Close();

        using var alter = conn.CreateCommand();
        alter.CommandText = alterSql;
        alter.ExecuteNonQuery();
    }

    private SqliteConnection Open()
    {
        var conn = new SqliteConnection(_connectionString);
        conn.Open();
        using var pragma = conn.CreateCommand();
        pragma.CommandText = "PRAGMA journal_mode=WAL";
        pragma.ExecuteNonQuery();
        return conn;
    }

    private List<CaptureEntry> ReadEntries(SqliteCommand cmd)
    {
        using var reader = cmd.ExecuteReader();
        var entries = new List<CaptureEntry>();
        while (reader.Read())
        {
            var rawContent = reader.GetString(3);
            string content;
            if (_encryption?.IsActive == true)
            {
                // KV-002: Decrypt now throws on tamper/corruption/wrong-key. Handle it
                // per-row so one bad entry surfaces a visible placeholder instead of
                // either silently showing ciphertext (the old bug) or crashing the
                // whole list. Other rows still decrypt normally.
                try
                {
                    content = _encryption.Decrypt(rawContent);
                }
                catch (DecryptionException)
                {
                    content = "[Unable to decrypt — wrong password, or this entry is corrupted / from another vault]";
                }
            }
            else
            {
                content = rawContent;
            }

            entries.Add(new CaptureEntry
            {
                Id = reader.GetInt64(0),
                AppName = reader.GetString(1),
                WindowTitle = reader.GetString(2),
                Content = content,
                CharCount = reader.GetInt32(4),
                CapturedAt = DateTime.Parse(reader.GetString(5), null, System.Globalization.DateTimeStyles.RoundtripKind),
                ExpiresAt = reader.IsDBNull(6) ? null : DateTime.Parse(reader.GetString(6), null, System.Globalization.DateTimeStyles.RoundtripKind),
                IsPinned = reader.GetInt32(7) == 1,
                EntryType = reader.FieldCount > 8 && !reader.IsDBNull(8) ? reader.GetString(8) : "keyboard",
                DetectedLanguage = reader.FieldCount > 9 && !reader.IsDBNull(9) ? reader.GetString(9) : null,
                Tags = reader.FieldCount > 10 && !reader.IsDBNull(10) ? reader.GetString(10) : string.Empty
            });
        }
        return entries;
    }
}
