// lang: C#, file: SpotifyService.cs, target: Windows 10+, .NET 6
// Uses Windows.Media.Control (SMTC) — works with any music app, no API key, no Premium needed
using System.IO;
using System.Net.Http;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Windows.Media.Imaging;
using Windows.Media.Control;
using Windows.Storage.Streams;

namespace SherStrapp.Services
{
    public class NowPlayingInfo
    {
        public string Title  { get; init; } = "";
        public string Artist { get; init; } = "";
        public bool   IsPlaying { get; init; }
        public BitmapImage? AlbumArt { get; init; }
    }

    public static class SpotifyService
    {
        private static GlobalSystemMediaTransportControlsSessionManager? _manager;

        public static event Action<NowPlayingInfo?>? NowPlayingChanged;

        // ── Init ──────────────────────────────────────────────────────────────
        public static async Task InitAsync()
        {
            _manager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
            _manager.CurrentSessionChanged += (_, _) => _ = RefreshAsync();
            await RefreshAsync();
        }

        // ── Poll (call every few seconds from VM) ─────────────────────────────
        public static async Task RefreshAsync()
        {
            if (_manager is null) return;

            var session = _manager.GetCurrentSession();
            if (session is null) { NowPlayingChanged?.Invoke(null); return; }

            try
            {
                var props  = await session.TryGetMediaPropertiesAsync();
                var pbInfo = session.GetPlaybackInfo();

                BitmapImage? art = null;
                if (props.Thumbnail is not null)
                {
                    using var stream = await props.Thumbnail.OpenReadAsync();
                    using var ms     = new MemoryStream();
                    await stream.AsStreamForRead().CopyToAsync(ms);
                    ms.Position = 0;
                    art = new BitmapImage();
                    art.BeginInit();
                    art.CacheOption  = BitmapCacheOption.OnLoad;
                    art.StreamSource = ms;
                    art.EndInit();
                    art.Freeze();
                }

                NowPlayingChanged?.Invoke(new NowPlayingInfo
                {
                    Title     = props.Title  ?? "",
                    Artist    = props.Artist ?? "",
                    IsPlaying = pbInfo.PlaybackStatus ==
                                GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing,
                    AlbumArt  = art
                });
            }
            catch { NowPlayingChanged?.Invoke(null); }
        }

        // ── Controls ──────────────────────────────────────────────────────────
        public static async Task PlayPauseAsync()
        {
            var s = _manager?.GetCurrentSession();
            if (s is null) return;
            if (s.GetPlaybackInfo().PlaybackStatus ==
                GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing)
                await s.TryPauseAsync();
            else
                await s.TryPlayAsync();
        }

        public static async Task NextAsync() =>
            await (_manager?.GetCurrentSession()?.TrySkipNextAsync() ?? Task.CompletedTask);

        public static async Task PrevAsync() =>
            await (_manager?.GetCurrentSession()?.TrySkipPreviousAsync() ?? Task.CompletedTask);
    }
}
