namespace Kapture.Services.CloudSync.Online;

/// <summary>
/// Persists the Online Vault session tokens. The default implementation is DPAPI-backed
/// (<see cref="DpapiOnlineTokenStore"/>); tests substitute an in-memory store.
/// </summary>
public interface IOnlineTokenStore
{
    OnlineTokens? Load();
    void Save(OnlineTokens tokens);
    void Clear();
}
