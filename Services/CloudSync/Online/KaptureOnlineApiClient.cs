using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace Kapture.Services.CloudSync.Online;

/// <summary>
/// <see cref="IKaptureOnlineApiClient"/> over <see cref="HttpClient"/>. Maps each backend endpoint
/// to a method, attaches the bearer session where required, and converts non-success responses to
/// <see cref="OnlineApiException"/> (carrying the status so callers can branch on 401/402).
/// </summary>
public sealed class KaptureOnlineApiClient : IKaptureOnlineApiClient
{
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _http;
    private readonly string _baseUrl;

    public KaptureOnlineApiClient(HttpClient http, string baseUrl)
    {
        ArgumentNullException.ThrowIfNull(http);
        ArgumentException.ThrowIfNullOrWhiteSpace(baseUrl);
        _http = http;
        _baseUrl = baseUrl.TrimEnd('/');
    }

    public async Task<bool> HealthAsync(CancellationToken ct = default)
    {
        using var resp = await RawSendAsync(HttpMethod.Get, "/health", session: null, body: null, ct);
        return resp.IsSuccessStatusCode;
    }

    public Task<OnlineSession> AuthSessionAsync(string googleIdToken, CancellationToken ct = default) =>
        SendAsync<OnlineSession>(HttpMethod.Post, "/auth/session", session: null, new { id_token = googleIdToken }, ct);

    public Task<OnlineSession> AuthWithCodeAsync(string code, string codeVerifier, string redirectUri, CancellationToken ct = default) =>
        SendAsync<OnlineSession>(HttpMethod.Post, "/auth/google", session: null,
            new { code, code_verifier = codeVerifier, redirect_uri = redirectUri }, ct);

    public Task<RefreshedSession> RefreshSessionAsync(string refreshToken, CancellationToken ct = default) =>
        SendAsync<RefreshedSession>(HttpMethod.Post, "/auth/refresh", session: null, new { refresh = refreshToken }, ct);

    public Task<MeResponse> GetMeAsync(string session, CancellationToken ct = default) =>
        SendAsync<MeResponse>(HttpMethod.Get, "/me", session, body: null, ct);

    public Task<BillingUrl> CreateCheckoutAsync(string session, CancellationToken ct = default) =>
        SendAsync<BillingUrl>(HttpMethod.Post, "/billing/checkout", session, body: null, ct);

    public Task<BillingUrl> CreatePortalAsync(string session, CancellationToken ct = default) =>
        SendAsync<BillingUrl>(HttpMethod.Post, "/billing/portal", session, body: null, ct);

    public Task<PresignedUrl> GetVaultPutUrlAsync(string session, CancellationToken ct = default) =>
        SendAsync<PresignedUrl>(HttpMethod.Post, "/vault/put-url", session, body: null, ct);

    public Task<PresignedUrl> GetVaultGetUrlAsync(string session, CancellationToken ct = default) =>
        SendAsync<PresignedUrl>(HttpMethod.Post, "/vault/get-url", session, body: null, ct);

    public async Task<VaultMetaResult> GetVaultMetaAsync(string session, CancellationToken ct = default)
    {
        using var resp = await RawSendAsync(HttpMethod.Get, "/vault/meta", session, body: null, ct);
        await ThrowIfFailedAsync(resp, ct);

        var body = await resp.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(body);
        if (doc.RootElement.ValueKind == JsonValueKind.Object &&
            doc.RootElement.TryGetProperty("exists", out var exists) &&
            exists.ValueKind == JsonValueKind.False)
        {
            return new VaultMetaResult(false, null);
        }

        var meta = JsonSerializer.Deserialize<VaultMeta>(body, JsonOpts);
        return new VaultMetaResult(meta is not null, meta);
    }

    public async Task PutVaultMetaAsync(string session, VaultMeta meta, CancellationToken ct = default)
    {
        using var resp = await RawSendAsync(HttpMethod.Put, "/vault/meta", session, meta, ct);
        await ThrowIfFailedAsync(resp, ct);
    }

    public Task<PresignedUrl> GetObjectPutUrlAsync(string session, string key, CancellationToken ct = default) =>
        SendAsync<PresignedUrl>(HttpMethod.Post, "/vault/object/put-url", session, new { key }, ct);

    public Task<PresignedUrl> GetObjectGetUrlAsync(string session, string key, CancellationToken ct = default) =>
        SendAsync<PresignedUrl>(HttpMethod.Post, "/vault/object/get-url", session, new { key }, ct);

    public async Task DeleteObjectAsync(string session, string key, CancellationToken ct = default)
    {
        using var resp = await RawSendAsync(HttpMethod.Post, "/vault/object/delete", session, new { key }, ct);
        await ThrowIfFailedAsync(resp, ct);
    }

    public Task<VaultObjectList> ListObjectsAsync(string session, CancellationToken ct = default) =>
        SendAsync<VaultObjectList>(HttpMethod.Get, "/vault/objects", session, body: null, ct);

    private async Task<T> SendAsync<T>(HttpMethod method, string path, string? session, object? body, CancellationToken ct)
    {
        using var resp = await RawSendAsync(method, path, session, body, ct);
        await ThrowIfFailedAsync(resp, ct);

        var result = await resp.Content.ReadFromJsonAsync<T>(JsonOpts, ct);
        return result ?? throw new OnlineApiException(resp.StatusCode, $"Empty response from {path}");
    }

    private async Task<HttpResponseMessage> RawSendAsync(
        HttpMethod method, string path, string? session, object? body, CancellationToken ct)
    {
        using var req = new HttpRequestMessage(method, _baseUrl + path);
        if (session is not null)
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", session);
        if (body is not null)
        {
            var json = JsonSerializer.Serialize(body, JsonOpts);
            req.Content = new StringContent(json, Encoding.UTF8, "application/json");
        }

        return await _http.SendAsync(req, ct);
    }

    private static async Task ThrowIfFailedAsync(HttpResponseMessage resp, CancellationToken ct)
    {
        if (resp.IsSuccessStatusCode) return;

        var body = await resp.Content.ReadAsStringAsync(ct);
        var message = TryExtractError(body) ?? $"{(int)resp.StatusCode} {resp.ReasonPhrase}";
        throw new OnlineApiException(resp.StatusCode, message);
    }

    /// <summary>Pull the <c>{"error":"..."}</c> message the Worker returns, if the body is that shape.</summary>
    private static string? TryExtractError(string body)
    {
        if (string.IsNullOrWhiteSpace(body)) return null;
        try
        {
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.ValueKind == JsonValueKind.Object &&
                doc.RootElement.TryGetProperty("error", out var error) &&
                error.ValueKind == JsonValueKind.String)
            {
                return error.GetString();
            }
        }
        catch (JsonException)
        {
            // Body wasn't JSON — fall back to the status line.
        }
        return null;
    }
}
