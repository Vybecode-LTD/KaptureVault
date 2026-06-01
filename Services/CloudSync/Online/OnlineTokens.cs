namespace Kapture.Services.CloudSync.Online;

/// <summary>
/// The persisted Online Vault session: the first-party session JWT, its refresh token, when the
/// session expires (UTC, with a safety skew already applied), and the user id. Held DPAPI-protected
/// on disk — never plaintext, never a Google or storage secret.
/// </summary>
public sealed record OnlineTokens(string Session, string Refresh, DateTime SessionExpiresAtUtc, string Uid);
