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

            // Send response to browser. The charset is explicit so the page renders as UTF-8 —
            // without it browsers fell back to a legacy encoding and mangled the glyphs.
            var responseHtml = BuildResultPage(string.IsNullOrEmpty(error), error);
            var buffer = Encoding.UTF8.GetBytes(responseHtml);
            context.Response.ContentType = "text/html; charset=utf-8";
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

    /// <summary>
    /// A small, branded result page for the loopback redirect. Uses inline styles (no &lt;style&gt;
    /// block) and HTML entities for the ✓/✗ glyphs so the C# source stays ASCII and the page renders
    /// reliably as UTF-8. Shared by Google Drive OAuth and the Online Vault sign-in.
    /// </summary>
    private static string BuildResultPage(bool success, string? error)
    {
        var color = success ? "#4ec9b0" : "#e08030";
        var mark = success ? "&#10003;" : "&#10007;";
        var heading = success ? "KaptureVault connected" : "Sign-in failed";
        var message = success
            ? "You&#39;re signed in. You can close this tab and return to KaptureVault."
            : WebUtility.HtmlEncode(string.IsNullOrEmpty(error) ? "Authorization was cancelled." : error);

        return "<!DOCTYPE html><html lang=\"en\"><head><meta charset=\"utf-8\"><title>KaptureVault</title></head>"
            + "<body style=\"background:#1e1e1e;margin:0;font-family:-apple-system,'Segoe UI',Roboto,sans-serif\">"
            + "<div style=\"max-width:420px;margin:64px auto;text-align:center;background:#252526;color:#e0e0e0;"
            + "padding:48px 56px;border:1px solid #3c3c3c;border-radius:12px\">"
            + $"<div style=\"font-size:48px;line-height:1;margin-bottom:16px;color:{color}\">{mark}</div>"
            + $"<h1 style=\"font-size:20px;margin:0 0 8px\">{heading}</h1>"
            + $"<p style=\"font-size:14px;color:#a0a0a0;margin:0;word-break:break-word\">{message}</p>"
            + "</div></body></html>";
    }
}
