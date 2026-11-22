// lang: C#, file: GameOverlayViewModel.cs, target: Windows 10+ MVVM overlay VM
// drives clock, now-playing, discord status, and roblox session info widgets
using System;
using System.Diagnostics;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SherStrapp.Services;

namespace SherStrapp.UI.ViewModels.Overlay
{
    public partial class GameOverlayViewModel : ObservableObject, IDisposable
    {
        // ── Clock ──────────────────────────────────────────────────────────────
        [ObservableProperty] private string _clockTime    = "";
        [ObservableProperty] private string _clockDate    = "";

        // ── Media ──────────────────────────────────────────────────────────────
        [ObservableProperty] private string        _trackTitle    = "Nothing playing";
        [ObservableProperty] private string        _trackArtist   = "";
        [ObservableProperty] private BitmapImage?  _albumArt;
        [ObservableProperty] private bool          _isPlaying;
        [ObservableProperty] private bool          _hasMedia;
        [ObservableProperty] private string        _playPauseGlyph = "▶";
        [ObservableProperty] private string        _mediaSource    = "";

        // ── Discord ────────────────────────────────────────────────────────────
        [ObservableProperty] private bool   _discordRunning;
        [ObservableProperty] private string _discordStatus = "Discord not detected";

        // ── Roblox session ─────────────────────────────────────────────────────
        [ObservableProperty] private string _robloxInfo = "";
        [ObservableProperty] private bool   _robloxRunning;

        // ── Internals ──────────────────────────────────────────────────────────
        private readonly DispatcherTimer _clockTimer;
        private readonly DispatcherTimer _statusTimer;

        public GameOverlayViewModel()
        {
            // clock ticks every second
            _clockTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _clockTimer.Tick += (_, _) => RefreshClock();
            _clockTimer.Start();

            // discord + roblox status every 5 seconds
            _statusTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
            _statusTimer.Tick += (_, _) => RefreshStatus();
            _statusTimer.Start();

            // media — subscribe to SMTC events and poll every 3 seconds
            SpotifyService.NowPlayingChanged += OnNowPlayingChanged;
            _ = SpotifyService.RefreshAsync();

            RefreshClock();
            RefreshStatus();
        }

        // ── Clock ──────────────────────────────────────────────────────────────
        private void RefreshClock()
        {
            var now = DateTime.Now;
            ClockTime = now.ToString("h:mm:ss tt");
            ClockDate = now.ToString("dddd, MMMM d");
        }

        // ── Media ──────────────────────────────────────────────────────────────
        private void OnNowPlayingChanged(NowPlayingInfo? info)
        {
            if (info is null)
            {
                HasMedia       = false;
                TrackTitle     = "Nothing playing";
                TrackArtist    = "";
                AlbumArt       = null;
                IsPlaying      = false;
                PlayPauseGlyph = "▶";
                MediaSource    = "";
                return;
            }

            HasMedia       = true;
            TrackTitle     = info.Title.Length > 0  ? info.Title  : "Unknown track";
            TrackArtist    = info.Artist.Length > 0 ? info.Artist : "";
            AlbumArt       = info.AlbumArt;
            IsPlaying      = info.IsPlaying;
            PlayPauseGlyph = info.IsPlaying ? "⏸" : "▶";
            MediaSource    = ""; // SMTC doesn't expose the app name without extra work
        }

        [RelayCommand]
        private async System.Threading.Tasks.Task PlayPause() =>
            await SpotifyService.PlayPauseAsync();

        [RelayCommand]
        private async System.Threading.Tasks.Task Next() =>
            await SpotifyService.NextAsync();

        [RelayCommand]
        private async System.Threading.Tasks.Task Prev() =>
            await SpotifyService.PrevAsync();

        // ── Discord / Roblox ───────────────────────────────────────────────────
        private void RefreshStatus()
        {
            // Discord: check process
            var discord = Process.GetProcessesByName("Discord");
            DiscordRunning = discord.Length > 0;
            DiscordStatus  = DiscordRunning
                ? "Discord is running"
                : "Discord not detected";

            // Roblox: check process and get uptime
            var roblox = Process.GetProcessesByName("RobloxPlayerBeta");
            RobloxRunning = roblox.Length > 0;
            if (RobloxRunning && roblox[0] != null)
            {
                try
                {
                    var elapsed = DateTime.Now - roblox[0].StartTime;
                    RobloxInfo = $"Session: {(int)elapsed.TotalMinutes}m {elapsed.Seconds}s";
                }
                catch { RobloxInfo = ""; }
            }
            else
            {
                RobloxInfo = "";
            }
        }

        // ── Overlay close ──────────────────────────────────────────────────────
        [RelayCommand]
        private static void Close() => OverlayService.Toggle();

        // ── Cleanup ────────────────────────────────────────────────────────────
        public void Dispose()
        {
            _clockTimer.Stop();
            _statusTimer.Stop();
            SpotifyService.NowPlayingChanged -= OnNowPlayingChanged;
        }
    }
}
