using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Web;

namespace Kapture.Services.CloudSync;

public class GoogleDriveProvider : ICloudStorageProvider
{
    // Fallback client ID used when no client_secret.json is present.
    // This is a client ID (public, not a secret) — safe to keep in source.
    private const string FallbackClientId = "232322018793-15r8pqq88382l8qap6jdtc81bdo111ok.apps.googleusercontent.com";
    private const int RedirectPort = 48721;
    private const string RedirectUri = "http://localhost:48721/";
    private const string Scope = "https://www.googleapis.com/auth/drive.file";
    private const string AuthEndpoint = "https://accounts.google.com/o/oauth2/v2/auth";
    private const string TokenEndpoint = "https://oauth2.googleapis.com/token";
    private const string DriveApiBase = "https://www.googleapis.com/drive/v3";
    private const string UploadApiBase = "https://www.googleapis.com/upload/drive/v3";
    private const string AppFolderName = "KaptureVault";
    private const int MaxRetries = 4;

    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromMinutes(5) };
    private CloudTokens? _tokens;
    private readonly string _clientId;
    private readonly string? _clientSecret;

    public string ProviderName => "Google Drive";
    public bool IsAuthenticated => _tokens?.AccessToken != null;
    public string? LastAuthError { get; private set; }

    public GoogleDriveProvider()
    {
        (_clientId, _clientSecret) = LoadCredentials();
        _tokens = CloudTokenStore.Load("google");
    }

    /// <summary>
    /// Loads OAuth client credentials. Checks for client_secret.json in
    /// %LOCALAPPDATA%\KaptureVault\ first, then alongside the executable.
    /// Falls back to the hardcoded client ID and the env var secret.
    /// </summary>
    private static (string clientId, string? clientSecret) LoadCredentials()
    {
        var appDataPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "KaptureVault", "client_secret.json");
        var sideBySidePath = Path.Combine(AppContext.BaseDirectory, "client_secret.json");

        foreach (var path in new[] { appDataPath, sideBySidePath })
        {
            if (!File.Exists(path)) continue;

            try
            {
                var json = File.ReadAllText(path);
                using var doc = System.Text.Json.JsonDocument.Parse(json);
                // Standard Google format: { "installed": { "client_id": "...", "client_secret": "..." } }
                if (doc.RootElement.TryGetProperty("installed", out var installed))
                {
                    var id = installed.GetProperty("client_id").GetString();
                    var secret = installed.GetProperty("client_secret").GetString();
                    if (!string.IsNullOrEmpty(id))
                        return (id, secret);
                }
            }
            catch
            {
                // Malformed JSON — fall through to next candidate
            }
        }

        // No valid file found — use fallback
        return (FallbackClientId, Environment.GetEnvironmentVariable("KAPTURE_GOOGLE_CLIENT_SECRET"));
    }

    public async Task<bool> AuthenticateAsync(CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(_clientSecret))
        {
            LastAuthError = "client_secret.json not found — place your Google OAuth credentials file in " +
                            $"%LOCALAPPDATA%\\KaptureVault\\";
            return false;
        }

        var codeVerifier = OAuthHelper.GenerateCodeVerifier();
        var codeChallenge = OAuthHelper.GenerateCodeChallenge(codeVerifier);

        var authUrl = $"{AuthEndpoint}?" +
            $"client_id={_clientId}&" +
            $"redirect_uri={HttpUtility.UrlEncode(RedirectUri)}&" +
            $"response_type=code&" +
            $"scope={HttpUtility.UrlEncode(Scope)}&" +
            $"code_challenge={codeChallenge}&" +
            $"code_challenge_method=S256&" +
            $"access_type=offline&" +
            $"prompt=consent";

        var code = await OAuthHelper.ListenForAuthCodeAsync(authUrl, RedirectPort, ct: ct);
        if (string.IsNullOrEmpty(code))
        {
            LastAuthError = "Authorization cancelled or timed out";
            return false;
        }

        var tokenParams = new Dictionary<string, string>
        {
            ["code"] = code,
            ["client_id"] = _clientId,
            ["client_secret"] = _clientSecret,
            ["redirect_uri"] = RedirectUri,
            ["grant_type"] = "authorization_code",
            ["code_verifier"] = codeVerifier
        };

        var tokenRequest = new FormUrlEncodedContent(tokenParams);

        var response = await _http.PostAsync(TokenEndpoint, tokenRequest, ct);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            LastAuthError = $"Token exchange failed ({(int)response.StatusCode} {response.StatusCode}): {TruncateError(body)}";
            return false;
        }

        var tokenJson = await response.Content.ReadFromJsonAsync<GoogleTokenResponse>(cancellationToken: ct);
        if (tokenJson == null)
        {
            LastAuthError = "Token exchange returned an empty response";
            return false;
        }

        _tokens = new CloudTokens
        {
            AccessToken = tokenJson.AccessToken,
            RefreshToken = tokenJson.RefreshToken ?? _tokens?.RefreshToken,
            ExpiresAt = DateTime.UtcNow.AddSeconds(tokenJson.ExpiresIn - 60)
        };
        CloudTokenStore.Save("google", _tokens);
        LastAuthError = null;
        return true;
    }

    public void SignOut()
    {
        _tokens = null;
        CloudTokenStore.Delete("google");
    }

    public async Task<string?> UploadFileAsync(string localPath, string remoteFileName, CancellationToken ct = default)
    {
        await EnsureTokenOrThrowAsync(ct);

        var existingId = await FindFileAsync(remoteFileName, ct);

        if (existingId != null)
            return await UpdateExistingFileAsync(existingId, localPath, ct);

        return await CreateNewFileAsync(localPath, remoteFileName, ct);
    }

    private async Task<string?> UpdateExistingFileAsync(string fileId, string localPath, CancellationToken ct)
    {
        return await WithRetryAsync(async () =>
        {
            using var stream = File.OpenRead(localPath);
            var request = new HttpRequestMessage(HttpMethod.Patch,
                $"{UploadApiBase}/files/{fileId}?uploadType=media&fields=id,md5Checksum");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _tokens!.AccessToken);
            request.Content = new StreamContent(stream);
            request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");

            var response = await _http.SendAsync(request, ct);
            await ThrowOnFailureAsync(response, "Update file", ct);

            var result = await response.Content.ReadFromJsonAsync<GoogleFileResponse>(cancellationToken: ct);

            VerifyChecksum(localPath, result?.Md5Checksum);

            return fileId;
        }, ct);
    }

    private async Task<string?> CreateNewFileAsync(string localPath, string remoteFileName, CancellationToken ct)
    {
        return await WithRetryAsync(async () =>
        {
            var folderId = await GetOrCreateAppFolderAsync(ct);

            var metadata = JsonSerializer.Serialize(new
            {
                name = remoteFileName,
                parents = folderId != null ? new[] { folderId } : Array.Empty<string>()
            });

            using var stream = File.OpenRead(localPath);
            var content = new MultipartContent("related");
            var metaPart = new StringContent(metadata, System.Text.Encoding.UTF8, "application/json");
            var filePart = new StreamContent(stream);
            filePart.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
            content.Add(metaPart);
            content.Add(filePart);

            var request = new HttpRequestMessage(HttpMethod.Post,
                $"{UploadApiBase}/files?uploadType=multipart&fields=id,md5Checksum");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _tokens!.AccessToken);
            request.Content = content;

            var response = await _http.SendAsync(request, ct);
            await ThrowOnFailureAsync(response, "Create file", ct);

            var result = await response.Content.ReadFromJsonAsync<GoogleFileResponse>(cancellationToken: ct);

            if (result?.Id != null && _tokens != null)
            {
                _tokens.RemoteFileId = result.Id;
                CloudTokenStore.Save("google", _tokens);
            }

            VerifyChecksum(localPath, result?.Md5Checksum);

            return result?.Id;
        }, ct);
    }

    public async Task<bool> DownloadFileAsync(string remoteFileId, string localPath, CancellationToken ct = default)
    {
        await EnsureTokenOrThrowAsync(ct);

        return await WithRetryAsync(async () =>
        {
            var request = new HttpRequestMessage(HttpMethod.Get,
                $"{DriveApiBase}/files/{remoteFileId}?alt=media");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _tokens!.AccessToken);

            var response = await _http.SendAsync(request, ct);
            await ThrowOnFailureAsync(response, "Download file", ct);

            await using var fs = File.Create(localPath);
            await response.Content.CopyToAsync(fs, ct);
            return true;
        }, ct);
    }

    public async Task<DateTime?> GetRemoteModifiedTimeAsync(string remoteFileId, CancellationToken ct = default)
    {
        await EnsureTokenOrThrowAsync(ct);

        var request = new HttpRequestMessage(HttpMethod.Get,
            $"{DriveApiBase}/files/{remoteFileId}?fields=modifiedTime");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _tokens!.AccessToken);

        var response = await _http.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode) return null;

        var result = await response.Content.ReadFromJsonAsync<GoogleFileResponse>(cancellationToken: ct);
        return result?.ModifiedTime;
    }

    public async Task<string?> FindFileAsync(string remoteFileName, CancellationToken ct = default)
    {
        await EnsureTokenOrThrowAsync(ct);

        if (_tokens?.RemoteFileId != null)
        {
            var check = new HttpRequestMessage(HttpMethod.Get,
                $"{DriveApiBase}/files/{_tokens.RemoteFileId}?fields=id,trashed");
            check.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _tokens!.AccessToken);
            var checkResp = await _http.SendAsync(check, ct);
            if (checkResp.IsSuccessStatusCode)
            {
                var fileInfo = await checkResp.Content.ReadFromJsonAsync<GoogleFileResponse>(cancellationToken: ct);
                if (fileInfo?.Trashed != true)
                    return _tokens.RemoteFileId;
            }
            _tokens.RemoteFileId = null;
        }

        var escapedName = remoteFileName.Replace("\\", "\\\\").Replace("'", "\\'");
        var query = HttpUtility.UrlEncode($"name='{escapedName}' and trashed=false");
        var request = new HttpRequestMessage(HttpMethod.Get,
            $"{DriveApiBase}/files?q={query}&fields=files(id,name)&pageSize=1");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _tokens!.AccessToken);

        var response = await _http.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode) return null;

        var result = await response.Content.ReadFromJsonAsync<GoogleFileListResponse>(cancellationToken: ct);
        var fileId = result?.Files?.FirstOrDefault()?.Id;

        if (fileId != null && _tokens != null)
        {
            _tokens.RemoteFileId = fileId;
            CloudTokenStore.Save("google", _tokens);
        }

        return fileId;
    }

    private async Task<string?> GetOrCreateAppFolderAsync(CancellationToken ct)
    {
        var escapedName = AppFolderName.Replace("'", "\\'");
        var query = HttpUtility.UrlEncode($"name='{escapedName}' and mimeType='application/vnd.google-apps.folder' and trashed=false");
        var request = new HttpRequestMessage(HttpMethod.Get,
            $"{DriveApiBase}/files?q={query}&fields=files(id)&pageSize=1");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _tokens!.AccessToken);

        var response = await _http.SendAsync(request, ct);
        if (response.IsSuccessStatusCode)
        {
            var result = await response.Content.ReadFromJsonAsync<GoogleFileListResponse>(cancellationToken: ct);
            if (result?.Files?.Count > 0)
                return result.Files[0].Id;
        }

        var metadata = JsonSerializer.Serialize(new
        {
            name = AppFolderName,
            mimeType = "application/vnd.google-apps.folder"
        });

        var createRequest = new HttpRequestMessage(HttpMethod.Post, $"{DriveApiBase}/files");
        createRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _tokens!.AccessToken);
        createRequest.Content = new StringContent(metadata, System.Text.Encoding.UTF8, "application/json");

        var createResponse = await _http.SendAsync(createRequest, ct);
        if (!createResponse.IsSuccessStatusCode)
            return null; // Fall back to Drive root

        var folder = await createResponse.Content.ReadFromJsonAsync<GoogleFileResponse>(cancellationToken: ct);
        return folder?.Id;
    }

    private async Task EnsureTokenOrThrowAsync(CancellationToken ct)
    {
        if (_tokens == null)
            throw new InvalidOperationException("Not authenticated — sign in to Google Drive first");

        if (DateTime.UtcNow < _tokens.ExpiresAt)
            return;

        if (string.IsNullOrEmpty(_tokens.RefreshToken))
            throw new InvalidOperationException("Token expired and no refresh token available — re-authenticate");

        var refreshParams = new Dictionary<string, string>
        {
            ["refresh_token"] = _tokens.RefreshToken,
            ["client_id"] = _clientId,
            ["grant_type"] = "refresh_token"
        };
        if (!string.IsNullOrEmpty(_clientSecret))
            refreshParams["client_secret"] = _clientSecret;

        var refreshRequest = new FormUrlEncodedContent(refreshParams);

        var response = await _http.PostAsync(TokenEndpoint, refreshRequest, ct);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            throw new InvalidOperationException($"Token refresh failed ({response.StatusCode}): {body}");
        }

        var tokenJson = await response.Content.ReadFromJsonAsync<GoogleTokenResponse>(cancellationToken: ct);
        if (tokenJson == null)
            throw new InvalidOperationException("Token refresh returned empty response");

        _tokens.AccessToken = tokenJson.AccessToken;
        _tokens.ExpiresAt = DateTime.UtcNow.AddSeconds(tokenJson.ExpiresIn - 60);
        if (!string.IsNullOrEmpty(tokenJson.RefreshToken))
            _tokens.RefreshToken = tokenJson.RefreshToken;

        CloudTokenStore.Save("google", _tokens);
    }

    private async Task<T> WithRetryAsync<T>(Func<Task<T>> operation, CancellationToken ct)
    {
        for (int attempt = 0; ; attempt++)
        {
            try
            {
                return await operation();
            }
            catch (DriveApiException ex) when (attempt < MaxRetries && ex.IsRetryable)
            {
                var delay = TimeSpan.FromMilliseconds(
                    Math.Min(60_000, 1000 * Math.Pow(2, attempt)) + Random.Shared.Next(500));
                await Task.Delay(delay, ct);

                if (ex.StatusCode == HttpStatusCode.Unauthorized)
                    await EnsureTokenOrThrowAsync(ct);
            }
        }
    }

    private static async Task ThrowOnFailureAsync(HttpResponseMessage response, string operation, CancellationToken ct)
    {
        if (response.IsSuccessStatusCode) return;

        var body = await response.Content.ReadAsStringAsync(ct);
        var isRetryable = response.StatusCode is
            HttpStatusCode.TooManyRequests or
            HttpStatusCode.ServiceUnavailable or
            HttpStatusCode.GatewayTimeout or
            HttpStatusCode.Unauthorized or
            HttpStatusCode.InternalServerError;

        // 403 with rate limit reason is retryable
        if (response.StatusCode == HttpStatusCode.Forbidden &&
            body.Contains("rateLimitExceeded", StringComparison.OrdinalIgnoreCase))
            isRetryable = true;

        throw new DriveApiException(
            $"{operation} failed ({(int)response.StatusCode} {response.StatusCode}): {TruncateError(body)}",
            response.StatusCode,
            isRetryable);
    }

    private static void VerifyChecksum(string localPath, string? remoteMd5)
    {
        if (string.IsNullOrEmpty(remoteMd5)) return;

        var localMd5 = Convert.ToHexStringLower(MD5.HashData(File.ReadAllBytes(localPath)));
        if (!localMd5.Equals(remoteMd5, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"Upload checksum mismatch: local={localMd5}, remote={remoteMd5}");
    }

    private static string TruncateError(string body) =>
        body.Length > 300 ? body[..300] + "..." : body;

    private class DriveApiException : Exception
    {
        public HttpStatusCode StatusCode { get; }
        public bool IsRetryable { get; }

        public DriveApiException(string message, HttpStatusCode statusCode, bool isRetryable)
            : base(message)
        {
            StatusCode = statusCode;
            IsRetryable = isRetryable;
        }
    }

    private class GoogleTokenResponse
    {
        [JsonPropertyName("access_token")] public string? AccessToken { get; set; }
        [JsonPropertyName("refresh_token")] public string? RefreshToken { get; set; }
        [JsonPropertyName("expires_in")] public int ExpiresIn { get; set; }
    }

    private class GoogleFileResponse
    {
        [JsonPropertyName("id")] public string? Id { get; set; }
        [JsonPropertyName("name")] public string? Name { get; set; }
        [JsonPropertyName("modifiedTime")] public DateTime? ModifiedTime { get; set; }
        [JsonPropertyName("md5Checksum")] public string? Md5Checksum { get; set; }
        [JsonPropertyName("trashed")] public bool? Trashed { get; set; }
    }

    private class GoogleFileListResponse
    {
        [JsonPropertyName("files")] public List<GoogleFileResponse>? Files { get; set; }
    }
}
