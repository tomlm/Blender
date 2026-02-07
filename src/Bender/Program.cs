using Avalonia;
using Consolonia;
using Bender.Shared.Models;

namespace Bender.Console
{
    public static class Program
    {
        private static int Main(string[] args)
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
                .StartWithConsoleLifetime(args);
            return 0;
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