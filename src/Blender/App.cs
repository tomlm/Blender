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
        /// Gets the application-wide view model containing command line arguments and input data.
        /// </summary>
        public AppViewModel AppViewModel { get; } = new();

        public override void OnFrameworkInitializationCompleted()
        {
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktopLifetime)
            {
                // Initialize the AppViewModel with command line arguments
                var args = desktopLifetime.Args ?? [];
                _ = AppViewModel.InitializeAsync(args);

                desktopLifetime.MainWindow = new MainWindow
                {
                    DataContext = new MainWindowViewModel(AppViewModel)
                };
            }

            base.OnFrameworkInitializationCompleted();
        }
    }
}
