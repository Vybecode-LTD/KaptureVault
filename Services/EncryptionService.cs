using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Kapture.Models;

namespace Kapture.Services;

public class EncryptionService : IEncryptionService
{
    private const int SaltSize = 16;
    private const int KeySize = 32; // AES-256
    private const int NonceSize = 12; // AES-GCM standard
    private const int TagSize = 16; // AES-GCM standard
    private const int Iterations = 100_000;
    private const string EncryptedPrefix = "ENC:";

    private static readonly string MetaDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".kapture");

    private static readonly string MetaPath = Path.Combine(MetaDir, "encryption.json");

    private byte[]? _key;

    public bool IsActive => _key != null;
    public bool IsConfigured => File.Exists(MetaPath);

    public void Configure(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        _key = DeriveKey(password, salt);

        // Store salt + password hash for verification
        var hash = SHA256.HashData(_key);
        var meta = new EncryptionMeta
        {
            Salt = Convert.ToBase64String(salt),
            KeyHash = Convert.ToBase64String(hash)
        };

        Directory.CreateDirectory(MetaDir);
        File.WriteAllText(MetaPath, JsonSerializer.Serialize(meta, new JsonSerializerOptions { WriteIndented = true }));
    }

    public bool Unlock(string password)
    {
        if (!IsConfigured) return false;

        var meta = LoadMeta();
        if (meta == null) return false;

        var salt = Convert.FromBase64String(meta.Salt);
        var candidateKey = DeriveKey(password, salt);
        var candidateHash = Convert.ToBase64String(SHA256.HashData(candidateKey));

        if (candidateHash != meta.KeyHash)
            return false;

        _key = candidateKey;
        return true;
    }

    public void Disable()
    {
        _key = null;
        if (File.Exists(MetaPath))
            File.Delete(MetaPath);
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

        // Only decrypt if it has our prefix
        if (!ciphertext.StartsWith(EncryptedPrefix))
            return ciphertext;

        try
        {
            var combined = Convert.FromBase64String(ciphertext[EncryptedPrefix.Length..]);
            if (combined.Length < NonceSize + TagSize)
                return ciphertext;

            var nonce = combined[..NonceSize];
            var tag = combined[NonceSize..(NonceSize + TagSize)];
            var encrypted = combined[(NonceSize + TagSize)..];
            var plaintext = new byte[encrypted.Length];

            using var aes = new AesGcm(_key, TagSize);
            aes.Decrypt(nonce, encrypted, tag, plaintext);

            return Encoding.UTF8.GetString(plaintext);
        }
        catch
        {
            return ciphertext; // Return as-is if decryption fails
        }
    }

    private static byte[] DeriveKey(string password, byte[] salt)
    {
        using var pbkdf2 = new Rfc2898DeriveBytes(password, salt, Iterations, HashAlgorithmName.SHA256);
        return pbkdf2.GetBytes(KeySize);
    }

    private static EncryptionMeta? LoadMeta()
    {
        try
        {
            var json = File.ReadAllText(MetaPath);
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
    }
}
