using System.CommandLine;
using System.IO;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Bender.ViewModels;

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

}
