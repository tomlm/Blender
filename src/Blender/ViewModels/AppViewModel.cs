using System;
using System.CommandLine;
using System.CommandLine.Parsing;
using System.IO;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Blender.ViewModels;

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
/// ViewModel that handles command line argument parsing and input data loading.
/// </summary>
public partial class AppViewModel : ViewModelBase
{
    [ObservableProperty]
    private string? _filePath;

    [ObservableProperty]
    private DataFormat _format = DataFormat.Auto;

    [ObservableProperty]
    private string? _inputData;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private bool _hasError;

    /// <summary>
    /// Parses command line arguments and loads input data.
    /// </summary>
    /// <param name="args">Command line arguments</param>
    /// <returns>True if successful, false if there was an error</returns>
    public async Task<bool> InitializeAsync(string[] args)
    {
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

        var rootCommand = new RootCommand("Blender - Visualize structured text data")
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

        // Determine format from flags
        Format = DetermineFormat(isXml, isYaml, isJson, isCsv);

        // Load input data
        if (file != null)
        {
            FilePath = file.FullName;
            if (!file.Exists)
            {
                HasError = true;
                ErrorMessage = $"File not found: {file.FullName}";
                return false;
            }

            try
            {
                InputData = await File.ReadAllTextAsync(file.FullName);
            }
            catch (Exception ex)
            {
                HasError = true;
                ErrorMessage = $"Error reading file: {ex.Message}";
                return false;
            }
        }
        else if (Console.IsInputRedirected)
        {
            // Read piped input from stdin
            try
            {
                InputData = await Console.In.ReadToEndAsync();
            }
            catch (Exception ex)
            {
                HasError = true;
                ErrorMessage = $"Error reading from stdin: {ex.Message}";
                return false;
            }
        }

        // Auto-detect format if not specified
        if (Format == DataFormat.Auto && !string.IsNullOrEmpty(InputData))
        {
            Format = DetectFormat(InputData, FilePath);
        }

        return true;
    }

    private static DataFormat DetermineFormat(bool isXml, bool isYaml, bool isJson, bool isCsv)
    {
        if (isXml) return DataFormat.Xml;
        if (isYaml) return DataFormat.Yaml;
        if (isJson) return DataFormat.Json;
        if (isCsv) return DataFormat.Csv;
        return DataFormat.Auto;
    }

    private static DataFormat DetectFormat(string data, string? filePath)
    {
        // First try to detect by file extension
        if (!string.IsNullOrEmpty(filePath))
        {
            var extension = Path.GetExtension(filePath).ToLowerInvariant();
            switch (extension)
            {
                case ".xml":
                    return DataFormat.Xml;
                case ".yml" or ".yaml":
                    return DataFormat.Yaml;
                case ".json":
                    return DataFormat.Json;
                case ".csv":
                    return DataFormat.Csv;
            }
        }

        // Then try to detect by content
        var trimmed = data.TrimStart();
        if (trimmed.StartsWith('<'))
        {
            return DataFormat.Xml;
        }
        if (trimmed.StartsWith('{') || trimmed.StartsWith('['))
        {
            return DataFormat.Json;
        }

        // Check for YAML-like structure (key: value patterns)
        if (trimmed.Contains(':') && !trimmed.Contains(','))
        {
            return DataFormat.Yaml;
        }

        // Check for CSV-like structure (comma-separated with consistent columns)
        if (trimmed.Contains(','))
        {
            return DataFormat.Csv;
        }

        return DataFormat.Auto;
    }
}
