using Bender.ViewModels;
using System.CommandLine;
using System.IO;
using System.Threading.Tasks;

namespace Bender.Shared.Models
{
    public class ArgumentsModel
    {

        private ArgumentsModel() { }

        public string? FilePath { get; set; }
        public DataFormat Format { get; set; } = DataFormat.Auto;
        public bool HasError { get; set; } = false;
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// Parses command line arguments.
        /// </summary>
        /// <param name="args">Command line arguments</param>
        /// <returns>True if successful, false if there was an error</returns>
        public static async Task<ArgumentsModel> ParseArgumentsAsync(string[] args)
        {
            var argsModel = new ArgumentsModel();
            // Positional argument for file (no switch required)
            var fileArgument = new Argument<FileInfo?>(
                name: "file",
                description: "Path to the input file to read")
            {
                Arity = ArgumentArity.ZeroOrOne // Makes it optional
            };

            var xmlOption = new Option<bool>(
                aliases: ["-x", "--xml"],
                description: "Force XML format");

            var yamlOption = new Option<bool>(
                aliases: ["-y", "--yml"],
                description: "Force YAML format");

            var jsonOption = new Option<bool>(
                aliases: ["-j", "--json"],
                description: "Force JSON format");

            var csvOption = new Option<bool>(
                aliases: ["-c", "--csv"],
                description: "Force CSV format");

            var rootCommand = new RootCommand("Bender - Visualize structured text data")
        {
            fileArgument,
            xmlOption,
            yamlOption,
            jsonOption,
            csvOption
        };

            FileInfo? file = null;
            bool isXml = false, isYaml = false, isJson = false, isCsv = false;

            rootCommand.SetHandler((fileValue, xmlValue, yamlValue, jsonValue, csvValue) =>
            {
                file = fileValue;
                isXml = xmlValue;
                isYaml = yamlValue;
                isJson = jsonValue;
                isCsv = csvValue;
            }, fileArgument, xmlOption, yamlOption, jsonOption, csvOption);

            var parseResult = await rootCommand.InvokeAsync(args);
            if (parseResult != 0)
            {
                argsModel.HasError = true;
                argsModel.ErrorMessage = "Failed to parse command line arguments.";
            }
            else
            {
                argsModel.FilePath = file?.FullName;
                argsModel.Format = DetermineFormat(isXml, isYaml, isJson, isCsv);
            }

            return argsModel;
        }


        /// <summary>
        /// Gets the help text for display in a dialog or console.
        /// </summary>
        public static string GetHelpText()
        {
            return """
            Bender - Visualize structured text data

            Usage: blender [options] <path>

            Options:
              <path>              Path to the input file to read
              -x, --xml           Force XML format
              -y, --yml           Force YAML format
              -j, --json          Force JSON format
              -c, --csv           Force CSV format
              -h, --help          Show this help message

            If no file is specified, data is read from stdin.
            If no format is specified, the format is auto-detected.
            """;
        }

        private static DataFormat DetermineFormat(bool isXml, bool isYaml, bool isJson, bool isCsv)
        {
            if (isXml) return DataFormat.Xml;
            if (isYaml) return DataFormat.Yaml;
            if (isJson) return DataFormat.Json;
            if (isCsv) return DataFormat.Csv;
            return DataFormat.Auto;
        }

    }
}
