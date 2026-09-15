using System.Configuration;
using System.Data;
using System.Windows;

namespace ApexBank;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        DispatcherUnhandledException += (sender, args) =>
        {
            MessageBox.Show(
                $"An unexpected application error occurred:\n\n{args.Exception.Message}\n\nStack Trace:\n{args.Exception.StackTrace}",
                "ApexBank - Error Recovered",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            args.Handled = true;
        };

        AppDomain.CurrentDomain.UnhandledException += (sender, args) =>
        {
            if (args.ExceptionObject is Exception ex)
            {
                MessageBox.Show(
                    $"A critical system error occurred:\n\n{ex.Message}",
                    "ApexBank - Critical Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        };
    }
}

