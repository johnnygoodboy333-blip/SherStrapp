// lang: C#, file: UI/ViewModels/Settings/SpotifyViewModel.cs

using System.Net;
using System.Net.Http;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.Input;
using SherStrapp.Services;

namespace SherStrapp.UI.ViewModels.Settings
{
    public class SpotifyViewModel : NotifyPropertyChangedViewModel, IDisposable
    {
        private readonly SpotifyService _spotify;
        private readonly HttpClient     _http = new();
        private BitmapImage?            _albumArt;
        private bool                    _isPremium = true; // assume premium until proven otherwise

        public SpotifyViewModel()
        {
            _spotify = new SpotifyService();
            _spotify.TrackChanged += OnTrackChanged;

            if (_spotify.IsLinked)
                _spotify.StartPolling();
        }

        // ── Auth ─────────────────────────────────────────────────────────────
        public ICommand LinkCommand   => new AsyncRelayCommand(LinkAsync);
        public ICommand UnlinkCommand => new RelayCommand(Unlink);

        public string ClientId
        {
            get => App.Settings.Prop.SpotifyClientId;
            set
            {
                App.Settings.Prop.SpotifyClientId = value;
                OnPropertyChanged(nameof(ClientId));
                OnPropertyChanged(nameof(CanLink));
            }
        }

        public bool IsLinked   => _spotify.IsLinked;
        public bool IsUnlinked => !IsLinked;
        public bool CanLink    => !string.IsNullOrWhiteSpace(ClientId) && IsUnlinked;

        private async Task LinkAsync()
        {
            bool ok = await _spotify.StartOAuthAsync();

            if (ok)
            {
                OnPropertyChanged(nameof(IsLinked));
                OnPropertyChanged(nameof(IsUnlinked));
                OnPropertyChanged(nameof(CanLink));
                _spotify.StartPolling();
            }
            else
            {
                MessageBox.Show(
                    "Spotify link failed. Make sure your Client ID is correct and the redirect URI\n" +
                    "http://localhost:58290/callback\nis added in your Spotify app settings.",
                    "SherStrapp", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void Unlink()
        {
            _spotify.StopPolling();
            _spotify.Unlink();
            CurrentTrack   = null;
            AlbumArt       = null;
            NowPlayingText = "Not linked";
            _isPremium     = true;
            OnPropertyChanged(nameof(IsLinked));
            OnPropertyChanged(nameof(IsUnlinked));
            OnPropertyChanged(nameof(CanLink));
            OnPropertyChanged(nameof(ControlsEnabled));
            OnPropertyChanged(nameof(FreeAccountNote));
        }

        // ── Premium detection ─────────────────────────────────────────────────
        public bool IsPremium
        {
            get => _isPremium;
            private set
            {
                _isPremium = value;
                OnPropertyChanged(nameof(IsPremium));
                OnPropertyChanged(nameof(ControlsEnabled));
                OnPropertyChanged(nameof(FreeAccountNote));
            }
        }

        // controls are only clickable when linked AND premium
        public bool ControlsEnabled => IsLinked && IsPremium;

        // shown below the controls when free account detected
        public string FreeAccountNote => IsPremium
            ? ""
            : "⚠ Playback controls require Spotify Premium. Now-playing display still works.";

        // ── Now playing ──────────────────────────────────────────────────────
        public SpotifyTrack? CurrentTrack { get; private set; }
        public string NowPlayingText { get; private set; } = "Nothing playing";

        public BitmapImage? AlbumArt
        {
            get => _albumArt;
            private set { _albumArt = value; OnPropertyChanged(nameof(AlbumArt)); }
        }

        public bool IsPlaying => CurrentTrack?.IsPlaying ?? false;
        public string PlayPauseIcon => IsPlaying ? "Pause24" : "Play24";

        private void OnTrackChanged(object? sender, SpotifyTrack? track)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                CurrentTrack = track;

                NowPlayingText = track is null
                    ? "Nothing playing"
                    : $"{track.Name}  —  {track.Artist}";

                if (track?.AlbumArt is not null)
                    LoadAlbumArt(track.AlbumArt);
                else
                    AlbumArt = null;

                OnPropertyChanged(nameof(NowPlayingText));
                OnPropertyChanged(nameof(IsPlaying));
                OnPropertyChanged(nameof(PlayPauseIcon));
                OnPropertyChanged(nameof(CurrentTrack));
            });
        }

        private void LoadAlbumArt(string url)
        {
            if (string.IsNullOrEmpty(url)) { AlbumArt = null; return; }
            Task.Run(async () =>
            {
                try
                {
                    var bytes = await _http.GetByteArrayAsync(url);
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        using var ms = new System.IO.MemoryStream(bytes);
                        var bmp = new BitmapImage();
                        bmp.BeginInit();
                        bmp.StreamSource     = ms;
                        bmp.CacheOption      = BitmapCacheOption.OnLoad;
                        bmp.DecodePixelWidth = 128;
                        bmp.EndInit();
                        bmp.Freeze();
                        AlbumArt = bmp;
                    });
                }
                catch { AlbumArt = null; }
            });
        }

        // ── Controls ─────────────────────────────────────────────────────────
        public ICommand PlayPauseCommand => new AsyncRelayCommand(PlayPauseAsync);
        public ICommand NextCommand      => new AsyncRelayCommand(NextAsync);
        public ICommand PrevCommand      => new AsyncRelayCommand(PrevAsync);

        private async Task PlayPauseAsync()
        {
            bool got403 = IsPlaying
                ? await _spotify.PauseAsync()
                : await _spotify.PlayAsync();

            if (got403) { IsPremium = false; return; }

            await Task.Delay(500);
            var track = await _spotify.GetCurrentTrackAsync();
            OnTrackChanged(this, track);
        }

        private async Task NextAsync()
        {
            bool got403 = await _spotify.NextAsync();
            if (got403) IsPremium = false;
        }

        private async Task PrevAsync()
        {
            bool got403;
            if (CurrentTrack is not null && CurrentTrack.ProgressMs > 3000)
                got403 = await _spotify.SeekAsync(0);
            else
                got403 = await _spotify.PrevAsync();

            if (got403) IsPremium = false;
        }

        // ── Setup instructions ────────────────────────────────────────────────
        public string SetupInstructions =>
            "1. Go to developer.spotify.com/dashboard\n" +
            "2. Log in → Create app (any name)\n" +
            "3. In the app settings, add redirect URI:\n" +
            "       http://localhost:58290/callback\n" +
            "4. Copy your Client ID and paste it above\n" +
            "5. Click Link — your browser will open Spotify login once\n\n" +
            "After linking, your refresh token is saved permanently.\n" +
            "You will never need to log in again.";

        public void Dispose()
        {
            _spotify.TrackChanged -= OnTrackChanged;
            _spotify.Dispose();
            _http.Dispose();
        }
    }
}
