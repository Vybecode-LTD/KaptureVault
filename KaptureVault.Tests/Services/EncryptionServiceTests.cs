using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Kapture.Services;
using Xunit;

namespace KaptureVault.Tests.Services;

/// <summary>
/// Covers the AES-256-GCM round-trip and, critically, KV-002: a decryption that
/// fails authentication (tamper / corruption / wrong key) must THROW
/// <see cref="DecryptionException"/> rather than silently returning the ciphertext
/// as if it were plaintext — which would defeat GCM's integrity guarantee.
///
/// All tests use a throwaway temp directory (via the base-directory seam) so they
/// never touch the real %LOCALAPPDATA%\KaptureVault\encryption.json.
/// </summary>
public class EncryptionServiceTests : IDisposable
{
    private readonly string _tempDir =
        Path.Combine(Path.GetTempPath(), "kvtest-" + Guid.NewGuid().ToString("N"));

    private EncryptionService NewConfiguredService(string password = "correct horse battery staple")
    {
        var svc = new EncryptionService(_tempDir);
        svc.Configure(password);
        return svc;
    }

    [Fact]
    public void EncryptThenDecrypt_RoundTripsToOriginal()
    {
        var svc = NewConfiguredService();

        var cipher = svc.Encrypt("hello vault");

        cipher.Should().StartWith("ENC:");
        cipher.Should().NotContain("hello");
        svc.Decrypt(cipher).Should().Be("hello vault");
    }

    [Fact]
    public void Decrypt_TamperedCiphertext_ThrowsDecryptionException()
    {
        // KV-002 regression: a flipped byte must be detected by the GCM auth tag
        // and surfaced as an error, not silently returned as ciphertext.
        var svc = NewConfiguredService();
        var cipher = svc.Encrypt("top secret");

        var tampered = FlipLastByte(cipher);

        var act = () => svc.Decrypt(tampered);
        act.Should().Throw<DecryptionException>();
    }

    [Fact]
    public void Decrypt_WrongKey_ThrowsDecryptionException()
    {
        // Content encrypted under one password, decrypted by a service unlocked with
        // another (mirrors a vault synced down under a different key).
        var encryptor = NewConfiguredService("password-one");
        var cipher = encryptor.Encrypt("cross-device secret");

        var otherDir = Path.Combine(Path.GetTempPath(), "kvtest-" + Guid.NewGuid().ToString("N"));
        try
        {
            var decryptor = new EncryptionService(otherDir);
            decryptor.Configure("password-two");

            var act = () => decryptor.Decrypt(cipher);
            act.Should().Throw<DecryptionException>();
        }
        finally
        {
            if (Directory.Exists(otherDir)) Directory.Delete(otherDir, true);
        }
    }

    [Fact]
    public void Decrypt_NonEncryptedInput_ReturnedAsIs()
    {
        var svc = NewConfiguredService();

        svc.Decrypt("just plain text").Should().Be("just plain text");
    }

    [Fact]
    public void Configure_StoresStrongKdfParams_AndDerivesWithThem()
    {
        // KV-006/T-11: new vaults must use a strong PBKDF2 count (OWASP 2023 floor = 600k)
        // and PERSIST it, so the key can be re-derived after the default changes again.
        var svc = NewConfiguredService("kdf-pass");

        using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(_tempDir, "encryption.json")));
        var root = doc.RootElement;
        var iterations = root.GetProperty("Iterations").GetInt32();
        iterations.Should().BeGreaterThanOrEqualTo(600_000, "PBKDF2 must meet the OWASP 2023 floor");

        // Prove Configure actually derived the key with the stored params (not some other count).
        var salt = Convert.FromBase64String(root.GetProperty("Salt").GetString()!);
        using var kdf = new Rfc2898DeriveBytes("kdf-pass", salt, iterations, HashAlgorithmName.SHA256);
        var expectedHash = Convert.ToBase64String(SHA256.HashData(kdf.GetBytes(32)));
        root.GetProperty("KeyHash").GetString().Should().Be(expectedHash);

        // And the round-trip still works.
        svc.Decrypt(svc.Encrypt("secret")).Should().Be("secret");
    }

    [Fact]
    public void Unlock_LegacyVaultWithoutStoredIterations_StillUnlocksAndDecrypts()
    {
        // A pre-T-11 vault: encryption.json had only Salt + KeyHash (no Iterations), key
        // derived at the old 100k count. After the bump it MUST still open and read its
        // existing entries — no data lockout.
        const string password = "legacy-pass";
        var salt = RandomNumberGenerator.GetBytes(16);
        byte[] legacyKey;
        using (var kdf = new Rfc2898DeriveBytes(password, salt, 100_000, HashAlgorithmName.SHA256))
            legacyKey = kdf.GetBytes(32);

        Directory.CreateDirectory(_tempDir);
        var legacyMeta =
            $$"""{"Salt":"{{Convert.ToBase64String(salt)}}","KeyHash":"{{Convert.ToBase64String(SHA256.HashData(legacyKey))}}"}""";
        File.WriteAllText(Path.Combine(_tempDir, "encryption.json"), legacyMeta);

        var svc = new EncryptionService(_tempDir);
        svc.IsConfigured.Should().BeTrue();
        svc.Unlock(password).Should().BeTrue("a legacy 100k vault must still unlock after the iteration bump");

        // An entry encrypted under the legacy key must remain readable.
        var legacyCipher = EncryptWithKey(legacyKey, "legacy entry");
        svc.Decrypt(legacyCipher).Should().Be("legacy entry");
    }

    // ── Phase 3 slice C: binary EncryptBytes/DecryptBytes (for screenshot blobs) ──

    [Fact]
    public void EncryptBytesThenDecryptBytes_RoundTripsToOriginal()
    {
        var svc = NewConfiguredService();
        var data = new byte[] { 0, 1, 2, 13, 10, 200, 255, 42 };

        var blob = svc.EncryptBytes(data);

        blob.Should().NotEqual(data, "the blob must be encrypted, not the raw bytes");
        blob.Length.Should().Be(12 + 16 + data.Length, "nonce(12) + GCM tag(16) + ciphertext");
        svc.DecryptBytes(blob).Should().Equal(data);
    }

    [Fact]
    public void DecryptBytes_Tampered_ThrowsDecryptionException()
    {
        var svc = NewConfiguredService();
        var blob = svc.EncryptBytes(new byte[] { 1, 2, 3, 4, 5 });
        blob[^1] ^= 0xFF; // corrupt the last ciphertext byte → GCM tag mismatch

        var act = () => svc.DecryptBytes(blob);
        act.Should().Throw<DecryptionException>();
    }

    [Fact]
    public void DecryptBytes_WrongKey_ThrowsDecryptionException()
    {
        var encryptor = NewConfiguredService("password-one");
        var blob = encryptor.EncryptBytes(new byte[] { 9, 8, 7, 6 });

        var otherDir = Path.Combine(Path.GetTempPath(), "kvtest-" + Guid.NewGuid().ToString("N"));
        try
        {
            var decryptor = new EncryptionService(otherDir);
            decryptor.Configure("password-two");

            var act = () => decryptor.DecryptBytes(blob);
            act.Should().Throw<DecryptionException>();
        }
        finally
        {
            if (Directory.Exists(otherDir)) Directory.Delete(otherDir, true);
        }
    }

    [Fact]
    public void EncryptBytes_WhenNotActive_Throws()
    {
        // No vault password configured → no key → must refuse (never silently emit plaintext).
        var svc = new EncryptionService(_tempDir);

        var act = () => svc.EncryptBytes(new byte[] { 1, 2, 3 });
        act.Should().Throw<InvalidOperationException>();
    }

    // Builds an ENC: blob in EncryptionService's wire format (nonce[12] + tag[16] + cipher).
    private static string EncryptWithKey(byte[] key, string plaintext)
    {
        var pt = Encoding.UTF8.GetBytes(plaintext);
        var nonce = RandomNumberGenerator.GetBytes(12);
        var cipher = new byte[pt.Length];
        var tag = new byte[16];
        using var aes = new AesGcm(key, 16);
        aes.Encrypt(nonce, pt, cipher, tag);
        var combined = new byte[12 + 16 + cipher.Length];
        nonce.CopyTo(combined, 0);
        tag.CopyTo(combined, 12);
        cipher.CopyTo(combined, 28);
        return "ENC:" + Convert.ToBase64String(combined);
    }

    private static string FlipLastByte(string encrypted)
    {
        var body = Convert.FromBase64String(encrypted["ENC:".Length..]);
        body[^1] ^= 0xFF; // corrupt the final ciphertext byte → GCM tag mismatch
        return "ENC:" + Convert.ToBase64String(body);
    }

    public void Dispose()
    {
        try { if (Directory.Exists(_tempDir)) Directory.Delete(_tempDir, true); } catch { }
    }
}
