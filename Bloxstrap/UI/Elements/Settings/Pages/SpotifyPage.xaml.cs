using SherStrapp.UI.ViewModels.Settings;

namespace SherStrapp.UI.Elements.Settings.Pages
{
    public partial class SpotifyPage : Wpf.Ui.Controls.UiPage
    {
        public SpotifyPage()
        {
            DataContext = new SpotifyViewModel();
            InitializeComponent();
        }
    }
}
