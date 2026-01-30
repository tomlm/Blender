using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Blender.ViewModels;
using Blender.Views;

namespace Blender
{
    public class App : Application
    {
        /// <summary>
        /// Gets the application-wide view model containing command line arguments.
        /// </summary>
        public AppViewModel AppViewModel { get; } = new();

        public override async void OnFrameworkInitializationCompleted()
        {
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktopLifetime)
            {
                // Parse command line arguments
                var args = desktopLifetime.Args ?? [];
                await AppViewModel.ParseArgumentsAsync(args);

                // Create the main window with its own data context
                var mainViewModel = new MainWindowViewModel();

                // Load data based on CLI arguments
                if (!string.IsNullOrEmpty(AppViewModel.FilePath))
                {
                    await mainViewModel.LoadFromFileAsync(AppViewModel.FilePath, AppViewModel.Format);
                }
                else
                {
                    await mainViewModel.LoadFromStdinAsync(AppViewModel.Format);
                }

                var mainWindow = new MainWindow
                {
                    DataContext = mainViewModel
                };
                mainViewModel.Window = mainWindow;
                desktopLifetime.MainWindow = mainWindow;
            }

            base.OnFrameworkInitializationCompleted();
        }
    }
}
