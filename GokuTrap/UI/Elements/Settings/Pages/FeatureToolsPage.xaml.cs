using System.Windows;
using GokuTrap.UI.ViewModels.Settings;

namespace GokuTrap.UI.Elements.Settings.Pages
{
    public partial class FeatureToolsPage
    {
        public static int RequestedTab { get; set; }
        public FeatureToolsPage()
        {
            InitializeComponent();
            Loaded += async (_, _) =>
            {
                if (DataContext is not FeatureToolsViewModel) DataContext = new FeatureToolsViewModel();
                FeatureTabs.SelectedIndex = RequestedTab;
                RequestedTab = 0;
                await ((FeatureToolsViewModel)DataContext).BackgroundCheckAsync();
            };
            Unloaded += (_, _) => { (DataContext as FeatureToolsViewModel)?.Dispose(); DataContext = null; };
        }
    }
}
