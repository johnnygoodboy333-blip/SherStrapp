// lang: C#, file: Services/LoadingScreenService.cs
// Copies a user-chosen image into the Roblox Modifications folder so it
// replaces the default loading/sky background on every launch.

using System.IO;

namespace SherStrapp.Services
{
    public static class LoadingScreenService
    {
        // Path inside the Modifications folder that Bloxstrap copies into the Roblox install.
        // This targets the loading screen background texture used by the Roblox client.
        private const string ModRelativePath = @"content\textures\ui\Loading\LoadingBackground.png";

        // Sky background (bonus — replaces the default blue sky in games that don't set one)
        private const string SkyTopPath    = @"content\sky\sky512_ft.tex";
        private const string SkyBottomPath = @"content\sky\sky512_dn.tex";

        public static string GetModTargetPath() =>
            Path.Combine(Paths.Modifications, ModRelativePath);

        /// <summary>
        /// Installs the custom loading screen image into the Modifications folder.
        /// Call this after the user picks a file, and also on every app launch via ApplyOnLaunch().
        /// </summary>
        public static bool Install(string sourcePath)
        {
            if (!File.Exists(sourcePath))
            {
                App.Logger.WriteLine("LoadingScreenService", $"Source image not found: {sourcePath}");
                return false;
            }

            try
            {
                string dest = GetModTargetPath();
                Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
                File.Copy(sourcePath, dest, overwrite: true);
                App.Logger.WriteLine("LoadingScreenService", $"Installed loading screen → {dest}");
                return true;
            }
            catch (Exception ex)
            {
                App.Logger.WriteException("LoadingScreenService::Install", ex);
                return false;
            }
        }

        /// <summary>
        /// Removes the custom loading screen mod file (restores Roblox default).
        /// </summary>
        public static void Uninstall()
        {
            try
            {
                string dest = GetModTargetPath();
                if (File.Exists(dest))
                {
                    File.Delete(dest);
                    App.Logger.WriteLine("LoadingScreenService", "Removed custom loading screen");
                }
            }
            catch (Exception ex)
            {
                App.Logger.WriteException("LoadingScreenService::Uninstall", ex);
            }
        }

        /// <summary>
        /// Called on app launch — re-applies the mod if a path is configured.
        /// Bloxstrap may clean the Modifications folder on update; this re-copies.
        /// </summary>
        public static void ApplyOnLaunch()
        {
            string path = App.Settings.Prop.CustomLoadingScreenPath;

            if (string.IsNullOrWhiteSpace(path))
                return;

            if (!App.Settings.Prop.UseCustomLoadingScreen)
                return;

            Install(path);
        }
    }
}
