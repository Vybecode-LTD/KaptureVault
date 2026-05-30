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
