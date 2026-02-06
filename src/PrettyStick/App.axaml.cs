using Avalonia.Markup.Xaml;

namespace PrettyStick.Console;

public partial class App : PrettyStick.App
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

}