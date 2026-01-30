namespace Blender.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    public MainWindowViewModel(AppViewModel appViewModel)
    {
        AppViewModel = appViewModel;
    }

    /// <summary>
    /// Gets the application view model containing command line arguments and input data.
    /// </summary>
    public AppViewModel AppViewModel { get; }

    public string Greeting { get; } = "Welcome to Avalonia!";
}
