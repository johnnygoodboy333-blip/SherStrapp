// lang: C#, file: UI/ViewModels/Settings/SherStrappViewModel.cs

using System.IO;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using SherStrapp.Services;

namespace SherStrapp.UI.ViewModels.Settings
{
    public class SherStrappViewModel : NotifyPropertyChangedViewModel
    {
        // ── Custom profile picture ────────────────────────────────────────────
        public ICommand BrowseProfilePictureCommand  => new RelayCommand(BrowseProfilePicture);
        public ICommand ClearProfilePictureCommand   => new RelayCommand(ClearProfilePicture);

        public string CustomProfilePicturePath
        {
            get => App.Settings.Prop.CustomProfilePicturePath;
            set
            {
                App.Settings.Prop.CustomProfilePicturePath = value;
                CustomProfilePictureService.Invalidate();
                OnPropertyChanged(nameof(CustomProfilePicturePath));
                OnPropertyChanged(nameof(ProfilePicturePreview));
                OnPropertyChanged(nameof(HasProfilePicture));
            }
        }

        public bool HasProfilePicture => CustomProfilePictureService.IsConfigured;

        public BitmapImage? ProfilePicturePreview => CustomProfilePictureService.GetImage();

        public bool UseCustomProfilePictureInRPC
        {
            get => App.Settings.Prop.UseCustomProfilePictureInRPC;
            set => App.Settings.Prop.UseCustomProfilePictureInRPC = value;
        }

        private void BrowseProfilePicture()
        {
            var dialog = new OpenFileDialog
            {
                Title  = "Select profile picture",
                Filter = "Image files|*.png;*.jpg;*.jpeg;*.bmp;*.webp|All files|*.*"
            };

            if (dialog.ShowDialog() != true)
                return;

            CustomProfilePicturePath = dialog.FileName;
        }

        private void ClearProfilePicture()
        {
            CustomProfilePicturePath = "";
        }

        // ── Accent colour ─────────────────────────────────────────────────────
        public string AccentColorHex
        {
            get => App.Settings.Prop.AccentColorHex;
            set
            {
                // validate it's a parsable colour before saving
                try
                {
                    var c = (Color)ColorConverter.ConvertFromString(value);
                    App.Settings.Prop.AccentColorHex = value;
                    OnPropertyChanged(nameof(AccentColorHex));
                    OnPropertyChanged(nameof(AccentColorPreview));
                }
                catch { /* ignore bad input */ }
            }
        }

        public SolidColorBrush AccentColorPreview
        {
            get
            {
                try { return new SolidColorBrush((Color)ColorConverter.ConvertFromString(AccentColorHex)); }
                catch { return Brushes.Gray; }
            }
        }

        public bool UseCustomBootstrapperLogo
        {
            get => App.Settings.Prop.UseCustomBootstrapperLogo;
            set => App.Settings.Prop.UseCustomBootstrapperLogo = value;
        }

        // ── Preset accent colours ─────────────────────────────────────────────
        public ICommand SetAccentSilverCommand  => new RelayCommand(() => AccentColorHex = "#C8C8C8");
        public ICommand SetAccentWhiteCommand   => new RelayCommand(() => AccentColorHex = "#FFFFFF");
        public ICommand SetAccentGoldCommand    => new RelayCommand(() => AccentColorHex = "#D4AF37");
        public ICommand SetAccentCyanCommand    => new RelayCommand(() => AccentColorHex = "#00D4FF");
        public ICommand SetAccentPurpleCommand  => new RelayCommand(() => AccentColorHex = "#9B59B6");

        // ── App version info ──────────────────────────────────────────────────
        public string VersionString => $"SherStrapp {App.Version}";
        public string BuildInfo     => App.IsProductionBuild ? "Release build" : "Development build";
    }
}
