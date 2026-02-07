using Avalonia;
using Bender.Shared.Models;
using System;

namespace Bender.Desktop;

class Program
{
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static int Main(string[] args)
    {
        var argsModel = ArgumentsModel.ParseArguments(args);

        if (argsModel.HelpRequested)
        {
            System.Console.WriteLine(ArgumentsModel.GetHelpText());
            return 0;
        }

        if (argsModel.HasError)
        {
            System.Console.Error.WriteLine(argsModel.ErrorMessage);
            return 1;
        }

        BuildAvaloniaApp()
            .StartWithClassicDesktopLifetime(args);
        return 0;
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
