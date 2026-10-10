using System.Windows;

namespace Scp603.Gui;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            MessageBox.Show(
                "Необработанная ошибка: " + args.ExceptionObject,
                "SCP-603",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        };

        base.OnStartup(e);
    }
}
