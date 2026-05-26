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
}
