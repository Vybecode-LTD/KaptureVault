namespace Kapture.Services.CloudSync.Online;

/// <summary>
/// The result of the desktop loopback-PKCE sign-in: the Google authorization code plus the PKCE
/// verifier and redirect URI the backend broker (<c>POST /auth/google</c>) needs to complete the
/// exchange with Google. The client holds no client secret — only these public/ephemeral values.
/// </summary>
public sealed record GoogleAuthCode(string Code, string CodeVerifier, string RedirectUri);
