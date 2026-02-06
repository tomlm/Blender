using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using PrettyStick.ViewModels;
using PrettyStick.Views;
using System.Threading.Tasks;

namespace PrettyStick
{
    public class App : Application
    {
        /// <summary>
        /// Gets the application-wide view model containing command line arguments.
        /// </summary>
        public AppViewModel AppViewModel { get; } = new();

        public override void OnFrameworkInitializationCompleted()
        {
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktopLifetime)
            {
                // Create the main window immediately so it can be shown
                var mainViewModel = new MainWindowViewModel();
                var mainWindow = new MainWindow
                {
                    DataContext = mainViewModel
                };
                mainViewModel.Window = mainWindow;
                desktopLifetime.MainWindow = mainWindow;

                // Parse arguments and load data asynchronously AFTER window is assigned
                _ = InitializeAsync(desktopLifetime.Args ?? [], mainViewModel);
            }

            base.OnFrameworkInitializationCompleted();
        }

        private async Task InitializeAsync(string[] args, MainWindowViewModel mainViewModel)
        {

            // Parse command line arguments
            await AppViewModel.ParseArgumentsAsync(args);

            // Load data based on CLI arguments
            if (!string.IsNullOrEmpty(AppViewModel.FilePath))
            {
                await mainViewModel.LoadFromFileAsync(AppViewModel.FilePath, AppViewModel.Format);
            }
            else
            {
                await mainViewModel.LoadFromStdinAsync(AppViewModel.Format);
            }
        }
    }
}

        