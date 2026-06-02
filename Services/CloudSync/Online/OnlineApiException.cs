using System.Net;

namespace Kapture.Services.CloudSync.Online;

/// <summary>
/// Thrown when the Online Vault backend returns a non-success status. Exposes the status code so
/// callers can branch on authentication (401) and subscription (402) without string-matching the
/// message.
/// </summary>
public sealed class OnlineApiException : Exception
{
    public HttpStatusCode StatusCode { get; }

    /// <summary>401 — session token missing, invalid, or expired (caller should refresh / re-auth).</summary>
    public bool IsUnauthorized => StatusCode == HttpStatusCode.Unauthorized;

    /// <summary>402 — authenticated but without an active subscription (paid feature gated).</summary>
    public bool IsPaymentRequired => (int)StatusCode == 402;

    /// <summary>403 — email/password login where the email isn't verified yet (Phase 5: prompt to verify).</summary>
    public bool IsForbidden => (int)StatusCode == 403;

    /// <summary>409 — registration for an email that already has an established account (Phase 5).</summary>
    public bool IsConflict => (int)StatusCode == 409;

    /// <summary>413 — the vault would exceed the storage quota (Phase 3: client trims oldest-first + retries).</summary>
    public bool IsPayloadTooLarge => (int)StatusCode == 413;

    public OnlineApiException(HttpStatusCode statusCode, string message) : base(message)
        => StatusCode = statusCode;
}
