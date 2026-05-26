using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Kapture.Services.CloudSync;

public class CloudTokens
{
    public string? AccessToken { get; set; }
    public string? RefreshToken { get; set; }
    public DateTime ExpiresAt { get; set; }
    public string? RemoteFileId { get; set; }
}

/// <summary>
/// Persists OAuth tokens per provider using Windows DPAPI (CurrentUser scope).
/// Tokens are protected on disk and cannot be read by other users or on other machines.
/// Migrates plaintext JSON tokens from older versions on first load.
/// </summary>
public static class CloudTokenStore
{
    private static readonly string Dir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "KaptureVault");

    private static string GetProtectedPath(string provider) =>
        Path.Combine(Dir, $"cloud_tokens_{provider.ToLowerInvariant()}.bin");

    private static string GetLegacyPath(string provider) =>
        Path.Combine(Dir, $"cloud_tokens_{provider.ToLowerInvariant()}.json");

    public static CloudTokens? Load(string provider)
    {
        var protectedPath = GetProtectedPath(provider);

        // Try loading DPAPI-protected file first
        if (File.Exists(protectedPath))
        {
            try
            {
                var protectedBytes = File.ReadAllBytes(protectedPath);
                var plainBytes = ProtectedData.Unprotect(
                    protectedBytes, null, DataProtectionScope.CurrentUser);
                var json = Encoding.UTF8.GetString(plainBytes);
                return JsonSerializer.Deserialize<CloudTokens>(json);
            }
            catch
            {
                // Corrupted or wrong user — cannot recover
                return null;
            }
        }

        // Migrate from legacy plaintext JSON if it exists
        var legacyPath = GetLegacyPath(provider);
        if (File.Exists(legacyPath))
        {
            try
            {
                var json = File.ReadAllText(legacyPath);
                var tokens = JsonSerializer.Deserialize<CloudTokens>(json);
                if (tokens != null)
                {
                    // Save as protected and remove plaintext
                    Save(provider, tokens);
                    try { File.Delete(legacyPath); } catch { /* best effort */ }
                }
                return tokens;
            }
            catch
            {
                return null;
            }
        }

        return null;
    }

    public static void Save(string provider, CloudTokens tokens)
    {
        Directory.CreateDirectory(Dir);
        var json = JsonSerializer.Serialize(tokens);
        var plainBytes = Encoding.UTF8.GetBytes(json);
        var protectedBytes = ProtectedData.Protect(
            plainBytes, null, DataProtectionScope.CurrentUser);
        File.WriteAllBytes(GetProtectedPath(provider), protectedBytes);
    }

    public static void Delete(string provider)
    {
        var protectedPath = GetProtectedPath(provider);
        if (File.Exists(protectedPath)) File.Delete(protectedPath);

        // Also clean up legacy file if present
        var legacyPath = GetLegacyPath(provider);
        if (File.Exists(legacyPath)) File.Delete(legacyPath);
    }
}
