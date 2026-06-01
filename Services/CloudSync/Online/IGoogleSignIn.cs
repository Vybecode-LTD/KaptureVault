namespace Kapture.Services.CloudSync.Online;

/// <summary>
/// Performs the interactive Google sign-in (system browser + loopback PKCE) and returns an
/// authorization code for the backend broker to exchange. Interface-backed so the account service
/// is unit-testable without a browser; the real implementation is Windows/loopback only.
/// </summary>
public interface IGoogleSignIn
{
    /// <summary>Opens the browser, waits for the loopback redirect, and returns the code (or null if cancelled).</summary>
    Task<GoogleAuthCode?> SignInAsync(CancellationToken ct = default);
}
