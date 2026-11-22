// lang: C#, file: GameOverlay.xaml.cs, target: Windows 10+ WPF overlay codebehind
using System.Windows;
using System.Windows.Input;
using SherStrapp.UI.ViewModels.Overlay;

namespace SherStrapp.UI.Elements.Overlay
{
    public partial class GameOverlay : Window
    {
        public GameOverlay()
        {
            InitializeComponent();
        }

        // Click on the dark scrim (outside panel) closes the overlay
        private void Scrim_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            // only close if click was on the scrim itself, not a child
            if (e.OriginalSource == sender)
                Hide();
        }

        // Escape or Ctrl+Alt+X from the overlay window itself
        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                Hide();
                e.Handled = true;
            }
        }

        protected override void OnClosed(System.EventArgs e)
        {
            if (DataContext is GameOverlayViewModel vm)
                vm.Dispose();

            base.OnClosed(e);
        }
    }
}
