// lang: C#, file: Services/SpotifyService.cs
// Full Spotify integration: PKCE OAuth, auto-refresh, now-playing, controls.
// Control methods return true if a 403 (Premium Required) was received.

using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;

namespace SherStrapp.Services
{
    public class SpotifyTrack
    {
        public string Name       { get; set; } = "";
        public string Artist     { get; set; } = "";
        public string AlbumArt   { get; set; } = "";
        public int    ProgressMs { get; set; }
        public int    DurationMs { get; set; }
        public bool   IsPlaying  { get; set; }
    }

    public class SpotifyTokenResponse
    {
        [JsonPropertyName("access_token")]  public string AccessToken  { get; set; } = "";
        [JsonPropertyName("refresh_token")] public string RefreshToken { get; set; } = "";
        [JsonPropertyName("expires_in")]    public int    ExpiresIn    { get; set; }
        [JsonPropertyName("token_type")]    public string TokenType    { get; set; } = "";
    }

    public class SpotifyService : IDisposable
    {
        private const string AuthBase    = "https://accounts.spotify.com";
        private const string ApiBase     = "https://api.spotify.com/v1/me/player";
        private const string Scopes      = "user-read-playback-state user-modify-playback-state user-read-currently-playing";
        private const string LocalPort   = "58290";
        private const string RedirectUri = $"http://localhost:{LocalPort}/callback";

        private readonly HttpClient _http = new();
        private string   _accessToken = "";
        private DateTime _tokenExpiry  = DateTime.MinValue;
        private CancellationTokenSource? _pollCts;

        public string ClientId  => App.Settings.Prop.SpotifyClientId;
        public bool   IsLinked  => !string.IsNullOrEmpty(App.Settings.Prop.SpotifyRefreshToken);

        public SpotifyTrack? CurrentTrack { get; private set; }
        public event EventHandler<SpotifyTrack?>? TrackChanged;

        // ── PKCE helpers ─────────────────────────────────────────────────────
        private static string B64Url(byte[] b) =>
            Convert.ToBase64String(b).TrimEnd('=').Replace('+', '-').Replace('/', '_');

        private static string MakeVerifier()
        {
            var b = new byte[64];
            RandomNumberGenerator.Fill(b);
            return B64Url(b);
        }

        private static string MakeChallenge(string verifier) =>
            B64Url(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));

        // ── OAuth ─────────────────────────────────────────────────────────────
        public async Task<bool> StartOAuthAsync()
        {
            if (string.IsNullOrWhiteSpace(ClientId)) return false;

            string verifier  = MakeVerifier();
            string challenge = MakeChallenge(verifier);
            string state     = Guid.NewGuid().ToString("N")[..16];

            string url =
                $"{AuthBase}/authorize" +
                $"?client_id={Uri.EscapeDataString(ClientId)}" +
                $"&response_type=code" +
                $"&redirect_uri={Uri.EscapeDataString(RedirectUri)}" +
                $"&scope={Uri.EscapeDataString(Scopes)}" +
                $"&code_challenge_method=S256" +
                $"&code_challenge={challenge}" +
                $"&state={state}";

            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });

            using var listener = new HttpListener();
            listener.Prefixes.Add($"http://localhost:{LocalPort}/");
            listener.Start();

            var ctx   = await listener.GetContextAsync().WaitAsync(TimeSpan.FromMinutes(3));
            var code  = ctx.Request.QueryString["code"];
            var rState = ctx.Request.QueryString["state"];

            var resp = ctx.Response;
            byte[] body = Encoding.UTF8.GetBytes(
                "<html><body style='font-family:sans-serif;padding:40px'>" +
                "<h2>✓ SherStrapp — Spotify linked!</h2><p>You can close this tab.</p></body></html>");
            resp.ContentLength64 = body.Length;
            await resp.OutputStream.WriteAsync(body);
            resp.Close();
            listener.Stop();

            if (code is null || rState != state) return false;

            return await ExchangeCodeAsync(code, verifier);
        }

        private async Task<bool> ExchangeCodeAsync(string code, string verifier)
        {
            var form = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"]    = "authorization_code",
                ["code"]          = code,
                ["redirect_uri"]  = RedirectUri,
                ["client_id"]     = ClientId,
                ["code_verifier"] = verifier,
            });

            var res  = await _http.PostAsync($"{AuthBase}/api/token", form);
            var json = await res.Content.ReadAsStringAsync();
            if (!res.IsSuccessStatusCode) return false;

            var tokens = JsonSerializer.Deserialize<SpotifyTokenResponse>(json);
            if (tokens is null) return false;

            ApplyTokens(tokens);
            return true;
        }

        private async Task<bool> RefreshAsync()
        {
            string refresh = App.Settings.Prop.SpotifyRefreshToken;
            if (string.IsNullOrEmpty(refresh)) return false;

            var form = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"]    = "refresh_token",
                ["refresh_token"] = refresh,
                ["client_id"]     = ClientId,
            });

            var res  = await _http.PostAsync($"{AuthBase}/api/token", form);
            var json = await res.Content.ReadAsStringAsync();
            if (!res.IsSuccessStatusCode) return false;

            var tokens = JsonSerializer.Deserialize<SpotifyTokenResponse>(json);
            if (tokens is null) return false;

            ApplyTokens(tokens);
            return true;
        }

        private void ApplyTokens(SpotifyTokenResponse t)
        {
            _accessToken = t.AccessToken;
            _tokenExpiry  = DateTime.UtcNow.AddSeconds(t.ExpiresIn - 60);
            if (!string.IsNullOrEmpty(t.RefreshToken))
                App.Settings.Prop.SpotifyRefreshToken = t.RefreshToken;
            App.Settings.Save();
        }

        public void Unlink()
        {
            App.Settings.Prop.SpotifyRefreshToken = "";
            App.Settings.Prop.SpotifyClientId     = "";
            _accessToken = "";
            _tokenExpiry  = DateTime.MinValue;
            CurrentTrack  = null;
            App.Settings.Save();
        }

        // ── API ───────────────────────────────────────────────────────────────
        private async Task<bool> EnsureTokenAsync()
        {
            if (!string.IsNullOrEmpty(_accessToken) && DateTime.UtcNow < _tokenExpiry)
                return true;
            return await RefreshAsync();
        }

        /// <summary>Returns true if the response was 403 (Premium Required).</summary>
        private async Task<bool> ApiAsync(HttpMethod method, string endpoint, string? body = null)
        {
            if (!await EnsureTokenAsync()) return false;

            var req = new HttpRequestMessage(method, ApiBase + endpoint);
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);

            if (body is not null)
                req.Content = new StringContent(body, Encoding.UTF8, "application/json");

            try
            {
                var res = await _http.SendAsync(req);
                return res.StatusCode == HttpStatusCode.Forbidden; // 403 = Premium Required
            }
            catch (Exception ex)
            {
                App.Logger.WriteException("SpotifyService::ApiAsync", ex);
                return false;
            }
        }

        // ── Playback ─────────────────────────────────────────────────────────
        public async Task<SpotifyTrack?> GetCurrentTrackAsync()
        {
            if (!await EnsureTokenAsync()) return null;

            var req = new HttpRequestMessage(HttpMethod.Get, ApiBase + "/currently-playing");
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);

            HttpResponseMessage res;
            try { res = await _http.SendAsync(req); }
            catch { return null; }

            if (res.StatusCode == HttpStatusCode.NoContent) return null;
            if (!res.IsSuccessStatusCode) return null;

            var json = await res.Content.ReadAsStringAsync();
            if (string.IsNullOrWhiteSpace(json)) return null;

            try
            {
                using var doc  = JsonDocument.Parse(json);
                var root   = doc.RootElement;
                var item   = root.GetProperty("item");
                var artists = string.Join(", ",
                    item.GetProperty("artists").EnumerateArray()
                        .Select(a => a.GetProperty("name").GetString() ?? ""));

                string artUrl = "";
                if (item.TryGetProperty("album", out var album) &&
                    album.TryGetProperty("images", out var imgs) &&
                    imgs.GetArrayLength() > 0)
                {
                    artUrl = imgs[Math.Min(1, imgs.GetArrayLength() - 1)]
                        .GetProperty("url").GetString() ?? "";
                }

                return new SpotifyTrack
                {
                    Name       = item.GetProperty("name").GetString() ?? "",
                    Artist     = artists,
                    AlbumArt   = artUrl,
                    ProgressMs = root.GetProperty("progress_ms").GetInt32(),
                    DurationMs = item.GetProperty("duration_ms").GetInt32(),
                    IsPlaying  = root.GetProperty("is_playing").GetBoolean(),
                };
            }
            catch { return null; }
        }

        /// <summary>Returns true if 403 (Premium Required).</summary>
        public Task<bool> PauseAsync()           => ApiAsync(HttpMethod.Put,  "/pause");
        public Task<bool> PlayAsync()            => ApiAsync(HttpMethod.Put,  "/play");
        public Task<bool> NextAsync()            => ApiAsync(HttpMethod.Post, "/next");
        public Task<bool> PrevAsync()            => ApiAsync(HttpMethod.Post, "/previous");
        public Task<bool> SeekAsync(int ms)      => ApiAsync(HttpMethod.Put,  $"/seek?position_ms={ms}");

        // ── Poll loop ─────────────────────────────────────────────────────────
        public void StartPolling()
        {
            if (_pollCts is not null) return;
            _pollCts = new CancellationTokenSource();
            Task.Run(() => PollLoopAsync(_pollCts.Token));
        }

        public void StopPolling()
        {
            _pollCts?.Cancel();
            _pollCts = null;
        }

        private async Task PollLoopAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    var track = await GetCurrentTrackAsync();
                    if (track?.Name != CurrentTrack?.Name || track?.IsPlaying != CurrentTrack?.IsPlaying)
                    {
                        CurrentTrack = track;
                        TrackChanged?.Invoke(this, track);
                    }
                    else
                    {
                        CurrentTrack = track;
                    }
                }
                catch { }

                await Task.Delay(3000, ct).ContinueWith(_ => { });
            }
        }

        public void Dispose()
        {
            StopPolling();
            _http.Dispose();
        }
    }
}
