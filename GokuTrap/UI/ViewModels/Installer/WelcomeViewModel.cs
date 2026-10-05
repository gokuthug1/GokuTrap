namespace GokuTrap.UI.ViewModels.Installer
{
    public class WelcomeViewModel : NotifyPropertyChangedViewModel
    {
        // formatting is done here instead of in xaml, it's just a bit easier
        public string MainText => String.Format(
            Strings.Installer_Welcome_MainText,
            "[github.com/gokuthug1/GokuTrap](https://github.com/gokuthug1/GokuTrap)",
            "[github.com/gokuthug1/GokuTrap](https://github.com/gokuthug1/GokuTrap)"
        );

        public bool CanContinue { get; set; } = false;
    }
}
