using Avalonia;
using Consolonia;
using Bender.Shared.Models;

namespace Bender.Console
{
    public static class Program
    {
        private static async Task<int> Main(string[] args)
        {
            // Parse command line arguments
            var argsModel = await ArgumentsModel.ParseArgumentsAsync(args);
            if (argsModel.HasError)
            {
                return -1;
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