// lang: C#, file: UI/Elements/Settings/Pages/LoadingScreenPage.xaml.cs
using SherStrapp.UI.ViewModels.Settings;

namespace SherStrapp.UI.Elements.Settings.Pages
{
    public partial class LoadingScreenPage
    {
        public LoadingScreenPage()
        {
            DataContext = new LoadingScreenViewModel();
            InitializeComponent();
        }
    }
}
