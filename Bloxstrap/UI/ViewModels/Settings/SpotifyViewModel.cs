// lang: C#, file: SpotifyViewModel.cs, target: Windows 10+
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.Input;
using SherStrapp.Services;

namespace SherStrapp.UI.ViewModels.Settings
{
    public partial class SpotifyViewModel : NotifyPropertyChangedViewModel
    {
        private readonly DispatcherTimer _pollTimer = new() { Interval = TimeSpan.FromSeconds(3) };

        // ── Bindable props ────────────────────────────────────────────────────
        private string _nowPlayingText = "Nothing playing";
        public string NowPlayingText
        {
            get => _nowPlayingText;
            set { _nowPlayingText = value; OnPropertyChanged(); }
        }

        private string _artistText = "";
        public string ArtistText
        {
            get => _artistText;
            set { _artistText = value; OnPropertyChanged(); }
        }

        private BitmapImage? _albumArt;
        public BitmapImage? AlbumArt
        {
            get => _albumArt;
            set { _albumArt = value; OnPropertyChanged(); }
        }

        private bool _isPlaying;
        public bool IsPlaying
        {
            get => _isPlaying;
            set { _isPlaying = value; OnPropertyChanged(); OnPropertyChanged(nameof(PlayPauseIcon)); }
        }

        public string PlayPauseIcon => IsPlaying ? "Pause24" : "Play24";

        private bool _hasSession;
        public bool HasSession
        {
            get => _hasSession;
            set { _hasSession = value; OnPropertyChanged(); }
        }

        public string StatusNote => "Reads from any music app — Spotify, Tidal, YouTube Music, etc.";

        // ── Commands ──────────────────────────────────────────────────────────
        [RelayCommand] private async Task PlayPause() => await SpotifyService.PlayPauseAsync();
        [RelayCommand] private async Task Next()      => await SpotifyService.NextAsync();
        [RelayCommand] private async Task Prev()      => await SpotifyService.PrevAsync();

        // ── Init ──────────────────────────────────────────────────────────────
        public SpotifyViewModel()
        {
            SpotifyService.NowPlayingChanged += OnNowPlayingChanged;
            _ = SpotifyService.InitAsync();

            _pollTimer.Tick += async (_, _) => await SpotifyService.RefreshAsync();
            _pollTimer.Start();
        }

        private void OnNowPlayingChanged(NowPlayingInfo? info)
        {
            App.Current.Dispatcher.Invoke(() =>
            {
                if (info is null)
                {
                    HasSession      = false;
                    NowPlayingText  = "Nothing playing";
                    ArtistText      = "";
                    AlbumArt        = null;
                    IsPlaying       = false;
                }
                else
                {
                    HasSession     = true;
                    NowPlayingText = info.Title;
                    ArtistText     = info.Artist;
                    AlbumArt       = info.AlbumArt;
                    IsPlaying      = info.IsPlaying;
                }
            });
        }
    }
}
