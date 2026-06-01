namespace Kapture.Services.CloudSync.Online;

/// <summary>
/// Google sign-in via the system browser + loopback PKCE, reusing <see cref="OAuthHelper"/>.
/// Requests an OIDC authorization code (scope "openid email") with NO client secret — the backend
/// broker (<c>POST /auth/google</c>) completes the token exchange server-side, so the desktop client
/// never holds a Google secret (this is the secret-less path that retires KV-007). Windows/loopback
/// only; covered by manual/E2E testing, not unit tests (same boundary as OAuthHelper/HotkeyService).
/// </summary>
public sealed class LoopbackGoogleSignIn : IGoogleSignIn
{
    private const string AuthEndpoint = "https://accounts.google.com/o/oauth2/v2/auth";
    private const string Scope = "openid email";

    private readonly OnlineVaultConfig _config;

    public LoopbackGoogleSignIn(OnlineVaultConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        _config = config;
    }

    public async Task<GoogleAuthCode?> SignInAsync(CancellationToken ct = default)
    {
        var redirectUri = $"http://localhost:{_config.LoopbackPort}/";
        var verifier = OAuthHelper.GenerateCodeVerifier();
        var challenge = OAuthHelper.GenerateCodeChallenge(verifier);

        var authUrl = $"{AuthEndpoint}?" +
            $"client_id={Uri.EscapeDataString(_config.GoogleClientId)}&" +
            $"redirect_uri={Uri.EscapeDataString(redirectUri)}&" +
            "response_type=code&" +
            $"scope={Uri.EscapeDataString(Scope)}&" +
            $"code_challenge={challenge}&" +
            "code_challenge_method=S256&" +
            "access_type=offline&" +
            "prompt=consent";

        var code = await OAuthHelper.ListenForAuthCodeAsync(authUrl, _config.LoopbackPort, ct: ct);
        return string.IsNullOrEmpty(code) ? null : new GoogleAuthCode(code, verifier, redirectUri);
    }
}
