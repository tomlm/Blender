using System.CommandLine;
using System.IO;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;

namespace PrettyStick.ViewModels;

/// <summary>
/// Supported data formats for input.
/// </summary>
public enum DataFormat
{
    Auto,
    Xml,
    Yaml,
    Json,
    Csv
}

/// <summary>
/// ViewModel that handles command line argument parsing.
/// </summary>
public partial class AppViewModel : ViewModelBase
{
    /// <summary>
    /// Gets the file path specified via -f/--file argument.
    /// </summary>
    [ObservableProperty]
    private string? _filePath;

    /// <summary>
    /// Gets the format specified via command line flags (-x, -y, -j, -c).
    /// </summary>
    [ObservableProperty]
    private DataFormat _format = DataFormat.Auto;

    /// <summary>
    /// Gets whether command line parsing failed.
    /// </summary>
    [ObservableProperty]
    private bool _hasError;

    /// <summary>
    /// Gets the error message if parsing failed.
    /// </summary>
    [ObservableProperty]
    private string? _errorMessage;

    /// <summary>
    /// Gets whether the help option was requested.
    /// </summary>
    [ObservableProperty]
    private bool _helpRequested;

    /// <summary>
    /// Parses command line arguments.
    /// </summary>
    /// <param name="args">Command line arguments</param>
    /// <returns>True if successful, false if there was an error</returns>
    public async Task<bool> ParseArgumentsAsync(string[] args)
    {
        // Check for help option before parsing
        foreach (var arg in args)
        {
            if (arg == "-h" || arg == "--help" || arg == "-?" || arg == "/?")
            {
                HelpRequested = true;
                System.Console.WriteLine(GetHelpText());
                return true;
            }
        }

        var fileOption = new Option<FileInfo?>(
            aliases: ["-f", "--file"],
            description: "Path to the input file to read");

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

        var rootCommand = new RootCommand("PrettyStick - Visualize structured text data")
        {
            fileOption,
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
        }, fileOption, xmlOption, yamlOption, jsonOption, csvOption);

        var parseResult = await rootCommand.InvokeAsync(args);
        if (parseResult != 0)
        {
            HasError = true;
            ErrorMessage = "Failed to parse command line arguments.";
            return false;
        }

        FilePath = file?.FullName;
        Format = DetermineFormat(isXml, isYaml, isJson, isCsv);

        return true;
    }

    /// <summary>
    /// Gets the help text for display in a dialog or console.
    /// </summary>
    public static string GetHelpText()
    {
        return """
            PrettyStick - Visualize structured text data

            Usage: blender [options]

            Options:
              -f, --file <path>   Path to the input file to read
              -x, --xml           Force XML format
              -y, --yml           Force YAML format
              -j, --json          Force JSON format
              -c, --csv           Force CSV format
              -h, --help          Show this help message

            If no file is specified, data is read from stdin.
            If no format is specified, the format is auto-detected.

            Keyboard Shortcuts:
              Ctrl+O              Open file
              Ctrl+W              Close window
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
