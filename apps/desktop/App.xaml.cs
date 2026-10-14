using System.IO;
using System.Windows;

namespace Hydra.Desktop;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        try
        {
            var window = new MainWindow();
            MainWindow = window;
            window.Show();
            if (e.Args.Contains("--smoke")) window.Loaded += async (_, _) => await window.SmokeAsync(e.Args);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Hydra could not start: {ex.Message}", "Hydra AI", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
        }
    }
}
