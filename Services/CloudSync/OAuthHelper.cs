using System.Diagnostics;
using System.Net;
using System.Security.Cryptography;
using System.Text;

namespace Kapture.Services.CloudSync;

/// <summary>
/// Shared OAuth2 PKCE helper for desktop apps.
/// Opens system browser, listens on localhost for the redirect callback.
/// </summary>
public static class OAuthHelper
{
    public static string GenerateCodeVerifier()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes)
            .Replace("+", "-").Replace("/", "_").TrimEnd('=');
    }

    public static string GenerateCodeChallenge(string codeVerifier)
    {
        var hash = SHA256.HashData(Encoding.ASCII.GetBytes(codeVerifier));
        return Convert.ToBase64String(hash)
            .Replace("+", "-").Replace("/", "_").TrimEnd('=');
    }

    /// <summary>
    /// Opens the browser to the auth URL, listens on localhost for the redirect,
    /// and returns the authorization code from the callback query string.
    /// </summary>
    public static async Task<string?> ListenForAuthCodeAsync(
        string authUrl, int port, string expectedPath = "/callback", CancellationToken ct = default)
    {
        var listener = new HttpListener();
        var prefix = $"http://localhost:{port}/";
        listener.Prefixes.Add(prefix);

        try
        {
            listener.Start();

            // Open browser
            Process.Start(new ProcessStartInfo(authUrl) { UseShellExecute = true });

            // Wait for callback
            var context = await listener.GetContextAsync().WaitAsync(TimeSpan.FromMinutes(3), ct);
            var code = context.Request.QueryString["code"];
            var error = context.Request.QueryString["error"];

            // Send response to browser
            var responseHtml = string.IsNullOrEmpty(error)
                ? "<html><body style='font-family:sans-serif;text-align:center;padding:60px'><h2>✓ KaptureVault Connected!</h2><p>You can close this window and return to KaptureVault.</p></body></html>"
                : $"<html><body style='font-family:sans-serif;text-align:center;padding:60px'><h2>✗ Authorization Failed</h2><p>{error}</p></body></html>";

            var buffer = Encoding.UTF8.GetBytes(responseHtml);
            context.Response.ContentType = "text/html";
            context.Response.ContentLength64 = buffer.Length;
            await context.Response.OutputStream.WriteAsync(buffer, ct);
            context.Response.Close();

            return code;
        }
        catch
        {
            return null;
        }
        finally
        {
            listener.Stop();
            listener.Close();
        }
    }
}
