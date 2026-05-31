using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Kapture.Models;

namespace Kapture.Services;

/// <summary>
/// Thrown when content carrying the encrypted prefix cannot be decrypted —
/// tampering, corruption, truncation, or a wrong password/key. Callers must
/// surface this to the user instead of treating ciphertext as plaintext (KV-002).
/// </summary>
public class DecryptionException : Exception
{
    public DecryptionException(string message, Exception? inner = null) : base(message, inner) { }
}

public class EncryptionService : IEncryptionService
{
    private const int SaltSize = 16;
    private const int KeySize = 32; // AES-256
    private const int NonceSize = 12; // AES-GCM standard
    private const int TagSize = 16; // AES-GCM standard
    // KV-006/T-11: PBKDF2-HMAC-SHA256 work factor. New vaults use the OWASP 2023 floor
    // (600k); the count is persisted in encryption.json so the key can always be
    // re-derived. Vaults created before T-11 stored no count and used 100k.
    private const int CurrentIterations = 600_000;
    private const int LegacyIterations = 100_000;
    private const string EncryptedPrefix = "ENC:";

    private readonly string _metaDir;
    private readonly string _metaPath;

    private byte[]? _key;

    /// <param name="baseDirectory">
    /// Directory that holds <c>encryption.json</c>. Defaults to
    /// <c>%LOCALAPPDATA%\KaptureVault</c>. Tests pass a temp directory so they never
    /// read or overwrite the real vault's encryption metadata.
    /// </param>
    public EncryptionService(string? baseDirectory = null)
    {
        _metaDir = baseDirectory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "KaptureVault");
        _metaPath = Path.Combine(_metaDir, "encryption.json");
    }

    public bool IsActive => _key != null;
    public bool IsConfigured => File.Exists(_metaPath);

    public void Configure(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        _key = DeriveKey(password, salt, CurrentIterations);

        // Store salt + key hash + the KDF params used, so the key can be re-derived even
        // after the iteration floor changes again (KV-006/T-11).
        var hash = SHA256.HashData(_key);
        var meta = new EncryptionMeta
        {
            Salt = Convert.ToBase64String(salt),
            KeyHash = Convert.ToBase64String(hash),
            Kdf = "PBKDF2-SHA256",
            Iterations = CurrentIterations
        };

        Directory.CreateDirectory(_metaDir);
        File.WriteAllText(_metaPath, JsonSerializer.Serialize(meta, new JsonSerializerOptions { WriteIndented = true }));
    }

    public bool Unlock(string password)
    {
        if (!IsConfigured) return false;

        var meta = LoadMeta();
        if (meta == null) return false;

        var salt = Convert.FromBase64String(meta.Salt);
        // Legacy vaults (pre-T-11) stored no iteration count → they used 100k.
        var iterations = meta.Iterations > 0 ? meta.Iterations : LegacyIterations;
        var candidateKey = DeriveKey(password, salt, iterations);
        var candidateHash = Convert.ToBase64String(SHA256.HashData(candidateKey));

        if (candidateHash != meta.KeyHash)
            return false;

        _key = candidateKey;
        return true;
    }

    public void Disable()
    {
        _key = null;
        if (File.Exists(_metaPath))
            File.Delete(_metaPath);
    }

    public string Encrypt(string plaintext)
    {
        if (_key == null || string.IsNullOrEmpty(plaintext))
            return plaintext;

        var plaintextBytes = Encoding.UTF8.GetBytes(plaintext);
        var nonce = RandomNumberGenerator.GetBytes(NonceSize);
        var ciphertext = new byte[plaintextBytes.Length];
        var tag = new byte[TagSize];

        using var aes = new AesGcm(_key, TagSize);
        aes.Encrypt(nonce, plaintextBytes, ciphertext, tag);

        // Format: nonce + tag + ciphertext, base64 encoded with prefix
        var combined = new byte[NonceSize + TagSize + ciphertext.Length];
        nonce.CopyTo(combined, 0);
        tag.CopyTo(combined, NonceSize);
        ciphertext.CopyTo(combined, NonceSize + TagSize);

        return EncryptedPrefix + Convert.ToBase64String(combined);
    }

    public string Decrypt(string ciphertext)
    {
        if (_key == null || string.IsNullOrEmpty(ciphertext))
            return ciphertext;

        // Only decrypt if it has our prefix; anything else is genuine plaintext.
        if (!ciphertext.StartsWith(EncryptedPrefix))
            return ciphertext;

        // KV-002: from here the value CLAIMS to be encrypted. Any failure is a real
        // error (tamper, corruption, wrong key) and must be surfaced — never swallowed
        // back to the caller as ciphertext, which would defeat AES-GCM's integrity.
        byte[] combined;
        try
        {
            combined = Convert.FromBase64String(ciphertext[EncryptedPrefix.Length..]);
        }
        catch (FormatException ex)
        {
            throw new DecryptionException("Encrypted content is malformed (invalid base64).", ex);
        }

        if (combined.Length < NonceSize + TagSize)
            throw new DecryptionException("Encrypted content is truncated or corrupted.");

        var nonce = combined[..NonceSize];
        var tag = combined[NonceSize..(NonceSize + TagSize)];
        var encrypted = combined[(NonceSize + TagSize)..];
        var plaintext = new byte[encrypted.Length];

        try
        {
            using var aes = new AesGcm(_key, TagSize);
            aes.Decrypt(nonce, encrypted, tag, plaintext);
        }
        catch (CryptographicException ex) // includes AuthenticationTagMismatchException
        {
            throw new DecryptionException(
                "Decryption failed — the content was tampered with, corrupted, or encrypted with a different password/key.", ex);
        }

        return Encoding.UTF8.GetString(plaintext);
    }

    private static byte[] DeriveKey(string password, byte[] salt, int iterations)
    {
        using var pbkdf2 = new Rfc2898DeriveBytes(password, salt, iterations, HashAlgorithmName.SHA256);
        return pbkdf2.GetBytes(KeySize);
    }

    private EncryptionMeta? LoadMeta()
    {
        try
        {
            var json = File.ReadAllText(_metaPath);
            return JsonSerializer.Deserialize<EncryptionMeta>(json);
        }
        catch
        {
            return null;
        }
    }

    private class EncryptionMeta
    {
        public string Salt { get; set; } = string.Empty;
        public string KeyHash { get; set; } = string.Empty;
        // KV-006/T-11: persisted KDF params. Absent in pre-T-11 files → Iterations
        // deserializes to 0, which Unlock treats as the legacy 100k count.
        public string Kdf { get; set; } = "PBKDF2-SHA256";
        public int Iterations { get; set; }
    }
}
