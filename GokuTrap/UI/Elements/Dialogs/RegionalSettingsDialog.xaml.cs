using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using GokuTrap.UI.ViewModels.Dialogs;

namespace GokuTrap.UI.Elements.Dialogs
{
    /// <summary>
    /// Interaction logic for LanguageSelectorDialog.xaml
    /// </summary>
    public partial class LanguageSelectorDialog
    {
        public LanguageSelectorDialog()
        {
            App.Logger.WriteLine("LanguageSelectorDialog", "Constructing LanguageSelectorDialog...");
            var viewModel = new RegionalSettingsViewModel();

            DataContext = viewModel;
            InitializeComponent();
            App.Logger.WriteLine("LanguageSelectorDialog", "Initialized component successfully.");

            viewModel.CloseRequestEvent += (_, _) =>
            {
                App.Logger.WriteLine("LanguageSelectorDialog", "CloseRequestEvent triggered, closing dialog.");
                Close();
            };

            Loaded += (_, _) => App.Logger.WriteLine("LanguageSelectorDialog", "Dialog loaded on screen.");
            Closed += (_, _) => App.Logger.WriteLine("LanguageSelectorDialog", "Dialog closed.");
        }
    }
}
