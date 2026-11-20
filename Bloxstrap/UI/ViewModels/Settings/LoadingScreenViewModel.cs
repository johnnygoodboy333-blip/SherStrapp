// lang: C#, file: UI/ViewModels/Settings/LoadingScreenViewModel.cs

using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using SherStrapp.Services;

namespace SherStrapp.UI.ViewModels.Settings
{
    public class LoadingScreenViewModel : NotifyPropertyChangedViewModel
    {
        public ICommand BrowseCommand   => new RelayCommand(Browse);
        public ICommand ClearCommand    => new RelayCommand(Clear);
        public ICommand ApplyNowCommand => new RelayCommand(ApplyNow);

        public bool UseCustomLoadingScreen
        {
            get => App.Settings.Prop.UseCustomLoadingScreen;
            set
            {
                App.Settings.Prop.UseCustomLoadingScreen = value;
                if (!value) LoadingScreenService.Uninstall();
                else        LoadingScreenService.ApplyOnLaunch();
            }
        }

        public string ImagePath
        {
            get => App.Settings.Prop.CustomLoadingScreenPath;
            set
            {
                App.Settings.Prop.CustomLoadingScreenPath = value;
                OnPropertyChanged(nameof(ImagePath));
                OnPropertyChanged(nameof(HasImage));
                OnPropertyChanged(nameof(PreviewImage));
            }
        }

        public bool HasImage => !string.IsNullOrWhiteSpace(ImagePath) && File.Exists(ImagePath);

        public BitmapImage? PreviewImage
        {
            get
            {
                if (!HasImage) return null;
                try
                {
                    var bmp = new BitmapImage();
                    bmp.BeginInit();
                    bmp.UriSource        = new Uri(ImagePath, UriKind.Absolute);
                    bmp.CacheOption      = BitmapCacheOption.OnLoad;
                    bmp.DecodePixelWidth = 600;
                    bmp.EndInit();
                    bmp.Freeze();
                    return bmp;
                }
                catch { return null; }
            }
        }

        private void Browse()
        {
            var dialog = new OpenFileDialog
            {
                Title  = "Select loading screen image",
                Filter = "Image files|*.png;*.jpg;*.jpeg;*.bmp|All files|*.*"
            };

            if (dialog.ShowDialog() != true) return;
            ImagePath = dialog.FileName;
        }

        private void Clear()
        {
            LoadingScreenService.Uninstall();
            ImagePath = "";
        }

        private void ApplyNow()
        {
            bool ok = LoadingScreenService.Install(ImagePath);
            MessageBox.Show(
                ok ? "Loading screen applied! Launch Roblox to see it."
                   : "Failed to apply — check the log for details.",
                "SherStrapp",
                MessageBoxButton.OK,
                ok ? MessageBoxImage.Information : MessageBoxImage.Error);
        }
    }
}
