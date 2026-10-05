using GokuTrap.UI.ViewModels.Settings;

namespace GokuTrap.UI.Elements.Settings.Pages
{
    /// <summary>
    /// Interaction logic for ModsPage.xaml
    /// </summary>
    public partial class ModsPage
    {
        private void OpenSoundPacks(object sender, System.Windows.RoutedEventArgs e)
        {
            FeatureToolsPage.RequestedTab = 1;
            if (System.Windows.Window.GetWindow(this) is MainWindow window) window.Navigate(typeof(FeatureToolsPage));
        }

        public ModsPage()
        {
            DataContext = new ModsViewModel();
            InitializeComponent();
        }
    }
}
