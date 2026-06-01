using System.Text.Json.Serialization;

namespace Kapture.Services.CloudSync.Online;

// Wire DTOs for the KaptureVault Online Vault backend (the Cloudflare Worker; separate repo
// kapturevault-backend). Grouped intentionally — they are one cohesive serialization contract,
// mirrored 1:1 from the Worker's JSON responses (src/index.ts there). JsonPropertyName is
// explicit so the exact wire keys are pinned regardless of serializer options.

/// <summary>Response of <c>POST /auth/session</c> — a first-party session + refresh token.</summary>
public sealed record OnlineSession(
    [property: JsonPropertyName("session")] string Session,
    [property: JsonPropertyName("refresh")] string Refresh,
    [property: JsonPropertyName("uid")] string Uid,
    [property: JsonPropertyName("expiresIn")] int ExpiresIn);

/// <summary>Response of <c>POST /auth/refresh</c> — a rotated session token.</summary>
public sealed record RefreshedSession(
    [property: JsonPropertyName("session")] string Session,
    [property: JsonPropertyName("expiresIn")] int ExpiresIn);

/// <summary>Subscription block inside <c>GET /me</c>.</summary>
public sealed record SubscriptionInfo(
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("currentPeriodEnd")] string? CurrentPeriodEnd);

/// <summary>Response of <c>GET /me</c> — profile + subscription + entitlement + storage used.</summary>
public sealed record MeResponse(
    [property: JsonPropertyName("uid")] string Uid,
    [property: JsonPropertyName("email")] string? Email,
    [property: JsonPropertyName("subscription")] SubscriptionInfo Subscription,
    [property: JsonPropertyName("entitled")] bool Entitled,
    [property: JsonPropertyName("storageUsed")] long StorageUsed);

/// <summary>Response of <c>POST /vault/{put,get}-url</c> — a short-lived presigned R2 URL.</summary>
public sealed record PresignedUrl(
    [property: JsonPropertyName("url")] string Url,
    [property: JsonPropertyName("expiresIn")] int ExpiresIn);

/// <summary>Response of <c>POST /billing/{checkout,portal}</c> — a Stripe URL to open in the browser.</summary>
public sealed record BillingUrl(
    [property: JsonPropertyName("url")] string Url);

/// <summary>
/// The small <c>vault.db.meta</c> object the client maintains alongside the uploaded vault, used
/// for last-writer-wins conflict checks (mirrors what Google Drive sync infers from modifiedTime).
/// </summary>
public sealed record VaultMeta(
    [property: JsonPropertyName("mtime")] string Mtime,
    [property: JsonPropertyName("sha256")] string Sha256,
    [property: JsonPropertyName("size")] long Size,
    [property: JsonPropertyName("version")] int Version = 1);

/// <summary>Result of <c>GET /vault/meta</c>: either no remote vault exists yet, or its current meta.</summary>
public sealed record VaultMetaResult(bool Exists, VaultMeta? Meta);
