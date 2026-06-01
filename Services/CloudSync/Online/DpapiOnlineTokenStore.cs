namespace Kapture.Services.CloudSync.Online;

/// <summary>
/// DPAPI-backed token store for the Online Vault, reusing <see cref="CloudTokenStore"/> under the
/// "online" provider key so the session sits beside the Drive tokens, CurrentUser-encrypted. Maps
/// the generic <see cref="CloudTokens"/> slots: AccessToken=session, RefreshToken=refresh,
/// ExpiresAt=session expiry, RemoteFileId=uid. Disk/DPAPI bound — exercised via the account
/// service's in-memory double in tests, like the rest of the CloudTokenStore boundary.
/// </summary>
public sealed class DpapiOnlineTokenStore : IOnlineTokenStore
{
    private const string Provider = "online";

    public OnlineTokens? Load()
    {
        var t = CloudTokenStore.Load(Provider);
        if (t?.AccessToken is null || t.RefreshToken is null || t.RemoteFileId is null)
            return null;
        return new OnlineTokens(t.AccessToken, t.RefreshToken, t.ExpiresAt, t.RemoteFileId);
    }

    public void Save(OnlineTokens tokens) =>
        CloudTokenStore.Save(Provider, new CloudTokens
        {
            AccessToken = tokens.Session,
            RefreshToken = tokens.Refresh,
            ExpiresAt = tokens.SessionExpiresAtUtc,
            RemoteFileId = tokens.Uid,
        });

    public void Clear() => CloudTokenStore.Delete(Provider);
}
