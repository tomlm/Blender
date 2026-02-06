using Avalonia;
using System.IO;
using PrettyStick.ViewModels;
using Consolonia;
using System.CommandLine.IO;

namespace PrettyStick.Console
{
    public static class Program
    {
        private static void Main(string[] args)
        {
            if (args.Contains("-h") || args.Contains("--help"))
            {
                System.Console.WriteLine(AppViewModel.GetHelpText());
                return;
            }

            BuildAvaloniaApp()
                .StartWithConsoleLifetime(args);
        }

        public static AppBuilder BuildAvaloniaApp()
        {
            return AppBuilder.Configure<App>()
                .UseConsolonia()
                .UseAutoDetectedConsole()
                .LogToException();
        }
    }
}