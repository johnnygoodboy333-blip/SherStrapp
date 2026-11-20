// lang: C#, file: UI/Elements/Settings/Pages/SherStrappPage.xaml.cs
using SherStrapp.UI.ViewModels.Settings;

namespace SherStrapp.UI.Elements.Settings.Pages
{
    public partial class SherStrappPage
    {
        public SherStrappPage()
        {
            DataContext = new SherStrappViewModel();
            InitializeComponent();
        }
    }
}
