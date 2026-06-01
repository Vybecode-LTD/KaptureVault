namespace Kapture.Services.CloudSync.Online;

/// <summary>
/// Public, non-secret configuration for the Online Vault backend: the Worker's base URL and the
/// public Google sign-in client id. Both are filled in at deploy time (placeholders until then),
/// exactly like <c>wrangler.toml [vars]</c> on the backend side. No secret ever lives here — the
/// session/refresh tokens are held DPAPI-protected in <see cref="CloudTokenStore"/>, and Google's
/// secret lives only on the Worker.
/// </summary>
public sealed class OnlineVaultConfig
{
    public const string ReplaceMarker = "REPLACE_WITH";

    public const string DefaultApiBaseUrl = "https://kapturevault-backend.kapture.workers.dev";
    public const string DefaultGoogleClientId = "232322018793-p6c6gmi0qug5ij427528gniclcol84rr.apps.googleusercontent.com";

    /// <summary>Base URL of the deployed Cloudflare Worker (no trailing slash needed).</summary>
    public string ApiBaseUrl { get; init; } = DefaultApiBaseUrl;

    /// <summary>The public OAuth client id of the dedicated sign-in client (audience of the ID token).</summary>
    public string GoogleClientId { get; init; } = DefaultGoogleClientId;

    /// <summary>Loopback port for the PKCE redirect (distinct from Drive's 48721 to avoid clashes).</summary>
    public int LoopbackPort { get; init; } = 48722;

    /// <summary>True once the deploy-time placeholders have been replaced with real values.</summary>
    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(ApiBaseUrl) &&
        !ApiBaseUrl.Contains(ReplaceMarker, StringComparison.Ordinal) &&
        !string.IsNullOrWhiteSpace(GoogleClientId) &&
        !GoogleClientId.Contains(ReplaceMarker, StringComparison.Ordinal);
}
