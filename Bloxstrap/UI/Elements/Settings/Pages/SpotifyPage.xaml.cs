// lang: C#, file: UI/Elements/Settings/Pages/SpotifyPage.xaml.cs
using SherStrapp.UI.ViewModels.Settings;

namespace SherStrapp.UI.Elements.Settings.Pages
{
    public partial class SpotifyPage
    {
        private readonly SpotifyViewModel _vm;

        public SpotifyPage()
        {
            _vm = new SpotifyViewModel();
            DataContext = _vm;
            InitializeComponent();
            Unloaded += (_, _) => _vm.Dispose();
        }
    }
}
