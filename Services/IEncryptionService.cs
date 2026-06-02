namespace Kapture.Services;

public interface IEncryptionService
{
    /// <summary>Whether encryption is currently active (password has been set and unlocked).</summary>
    bool IsActive { get; }

    /// <summary>Whether a password has been configured (salt exists in settings).</summary>
    bool IsConfigured { get; }

    /// <summary>Set up encryption with a new password. Derives key and stores salt/hash.</summary>
    void Configure(string password);

    /// <summary>Unlock encryption with the existing password. Returns false if wrong password.</summary>
    bool Unlock(string password);

    /// <summary>Remove encryption, decrypting all existing entries.</summary>
    void Disable();

    /// <summary>Encrypt plaintext content. Returns base64-encoded ciphertext. No-op if not active.</summary>
    string Encrypt(string plaintext);

    /// <summary>Decrypt ciphertext. Returns plaintext. No-op if not active.</summary>
    string Decrypt(string ciphertext);

    /// <summary>
    /// Public, non-secret key-derivation parameters (salt + iteration count + KDF name) read from the
    /// stored encryption metadata, so another device or the web vault can re-derive the AES key from
    /// the user's password. Returns null when no vault password is configured. Does NOT require unlock.
    /// </summary>
    VaultKdfInfo? GetKdfInfo();
}

/// <summary>
/// Public PBKDF2 parameters carried in <c>vault.db.meta</c> for cross-device / web-vault key
/// derivation. None of these are secret (the salt and iteration count are public by PBKDF2 design;
/// the key still requires the user's password).
/// </summary>
public sealed record VaultKdfInfo(string Kdf, int Iterations, string SaltBase64);
