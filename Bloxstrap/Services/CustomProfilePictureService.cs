// lang: C#, file: Services/CustomProfilePictureService.cs
// Provides a resolved ImageSource for the custom profile picture.
// Used by the Discord RPC integration and the SherStrapp settings page.

using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace SherStrapp.Services
{
    public static class CustomProfilePictureService
    {
        private static BitmapImage? _cached;
        private static string        _cachedPath = "";

        /// <summary>
        /// Returns a BitmapImage for the custom profile picture, or null if none is set / the file is invalid.
        /// Result is cached until the path changes.
        /// </summary>
        public static BitmapImage? GetImage()
        {
            string path = App.Settings.Prop.CustomProfilePicturePath;

            if (string.IsNullOrWhiteSpace(path))
                return null;

            if (path == _cachedPath && _cached is not null)
                return _cached;

            if (!File.Exists(path))
            {
                App.Logger.WriteLine("CustomProfilePictureService", $"Profile picture not found at '{path}'");
                return null;
            }

            try
            {
                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.UriSource       = new Uri(path, UriKind.Absolute);
                bmp.CacheOption     = BitmapCacheOption.OnLoad;
                bmp.DecodePixelWidth = 256; // cap size, saves memory
                bmp.EndInit();
                bmp.Freeze();

                _cached     = bmp;
                _cachedPath = path;
                return bmp;
            }
            catch (Exception ex)
            {
                App.Logger.WriteException("CustomProfilePictureService::GetImage", ex);
                return null;
            }
        }

        /// <summary>
        /// Clears the cached image so the next call to GetImage() re-reads from disk.
        /// Call this after the user picks a new file.
        /// </summary>
        public static void Invalidate()
        {
            _cached     = null;
            _cachedPath = "";
        }

        /// <summary>
        /// Returns true if a valid custom profile picture is configured.
        /// </summary>
        public static bool IsConfigured =>
            !string.IsNullOrWhiteSpace(App.Settings.Prop.CustomProfilePicturePath)
            && File.Exists(App.Settings.Prop.CustomProfilePicturePath);
    }
}
