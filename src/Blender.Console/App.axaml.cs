using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using PrettyStick.ViewModels;
using PrettyStick.Views;

namespace PrettyStick.Console;

public partial class App : PrettyStick.App
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

}