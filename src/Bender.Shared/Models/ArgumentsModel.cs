using Bender.ViewModels;
using System;
using System.CommandLine;
using System.CommandLine.Parsing;
using System.IO;
using System.Linq;

namespace Bender.Shared.Models
{
    public class ArgumentsModel
    {
        private ArgumentsModel() { }

        public string? FilePath { get; set; }
        public DataFormat Format { get; set; } = DataFormat.Auto;
        public bool HasError { get; set; } = false;
        public string? ErrorMessage { get; set; }
        public bool HelpRequested { get; set; } = false;

        private static readonly Argument<FileInfo?> FileArgument = new(
            name: "file",
            description: "Path to the input file to read")
        {
            Arity = ArgumentArity.ZeroOrOne
        };

        private static readonly Option<bool> XmlOption = new(["-x", "--xml"], "Force XML format");
        private static readonly Option<bool> YamlOption = new(["-y", "--yml"], "Force YAML format");
        private static readonly Option<bool> JsonOption = new(["-j", "--json"], "Force JSON format");
        private static readonly Option<bool> CsvOption = new(["-c", "--csv"], "Force CSV format");

        private static readonly RootCommand RootCommand = new("Bender - Visualize structured text data")
        {
            FileArgument, XmlOption, YamlOption, JsonOption, CsvOption
        };

        /// <summary>
        /// Parses command line arguments synchronously.
        /// </summary>
        public static ArgumentsModel ParseArguments(string[] args)
        {
            // Check for help request before parsing
            if (args.Any(a => a is "-h" or "--help" or "-?" or "/?"))
            {
                return new ArgumentsModel
                {
                    HelpRequested = true
                };
            }

            var parseResult = RootCommand.Parse(args);

            // Check for parse errors
            if (parseResult.Errors.Count > 0)
            {
                return new ArgumentsModel
                {
                    HasError = true,
                    ErrorMessage = string.Join(Environment.NewLine, parseResult.Errors.Select(e => e.Message))
                };
            }

            // Check for unrecognized options (tokens that look like switches)
            var file = parseResult.GetValueForArgument(FileArgument);
            var unknownSwitches = parseResult.UnmatchedTokens
                .Concat(file?.Name is { } name && name.StartsWith('-') ? [name] : [])
                .Where(t => t.StartsWith('-'))
                .ToList();

            if (unknownSwitches.Count > 0)
            {
                return new ArgumentsModel
                {
                    HasError = true,
                    ErrorMessage = $"Unrecognized option(s): {string.Join(", ", unknownSwitches)}"
                };
            }

            // Extract values directly from parse result
            var isXml = parseResult.GetValueForOption(XmlOption);
            var isYaml = parseResult.GetValueForOption(YamlOption);
            var isJson = parseResult.GetValueForOption(JsonOption);
            var isCsv = parseResult.GetValueForOption(CsvOption);
            
            return new ArgumentsModel
            {
                FilePath = file?.FullName,
                Format = DetermineFormat(isXml, isYaml, isJson, isCsv)
            };
        }

        /// <summary>
        /// Gets the help text for display in a dialog or console.
        /// </summary>
        public static string GetHelpText()
        {
            return """
            Bender - Visualize structured text data

            Usage: bender [options] [file]

            Arguments:
              file                Path to the input file to read

            Options:
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
