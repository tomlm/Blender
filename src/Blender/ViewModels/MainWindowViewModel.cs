using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using System.Xml.Linq;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CsvHelper;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using YamlDotNet.RepresentationModel;

namespace PrettyStick.ViewModels;

/// <summary>
/// The view mode for displaying data.
/// </summary>
public enum ViewMode
{
    Objects,
    Raw
}

public partial class MainWindowViewModel : ViewModelBase
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
    /// Gets or sets whether the view model is currently loading data.
    /// </summary>
    [ObservableProperty]
    private bool _isLoading;

    /// <summary>
    /// Gets or sets the current loading progress (bytes read).
    /// </summary>
    [ObservableProperty]
    private long _loadingProgress;

    /// <summary>
    /// Gets or sets the maximum value for loading progress (total file size).
    /// </summary>
    [ObservableProperty]
    private long _loadingMaximum = 100;

    /// <summary>
    /// Gets or sets the loading status message.
    /// </summary>
    [ObservableProperty]
    private string? _loadingStatus;

    /// <summary>
    /// Gets the deserialized data object based on the detected format.
    /// Can be JsonNode, XmlDocument, YamlStream, or List&lt;dynamic&gt; for CSV.
    /// </summary>
    [ObservableProperty]
    private object? _data;

    /// <summary>
    /// Gets whether there is data loaded to display.
    /// </summary>
    public bool HasData => Data != null || !string.IsNullOrEmpty(InputData);

    /// <summary>
    /// Gets the format name as a string for syntax highlighting.
    /// </summary>
    public string? FormatName => Format switch
    {
        DataFormat.Json => "json",
        DataFormat.Yaml => "yaml",
        DataFormat.Xml => "xml",
        DataFormat.Csv => "csv",
        _ => null
    };

    partial void OnDataChanged(object? value) => OnPropertyChanged(nameof(HasData));
    partial void OnInputDataChanged(string? value) => OnPropertyChanged(nameof(HasData));
    partial void OnFormatChanged(DataFormat value) => OnPropertyChanged(nameof(FormatName));

    /// <summary>
    /// Gets or sets the current view mode.
    /// </summary>
    [ObservableProperty]
    private ViewMode _viewMode = ViewMode.Objects;

    /// <summary>
    /// Gets whether the Objects view is active.
    /// </summary>
    public bool IsObjectsView => ViewMode == ViewMode.Objects;

    /// <summary>
    /// Gets whether the Raw view is active.
    /// </summary>
    public bool IsRawView => ViewMode == ViewMode.Raw;

    partial void OnViewModeChanged(ViewMode value)
    {
        OnPropertyChanged(nameof(IsObjectsView));
        OnPropertyChanged(nameof(IsRawView));
    }

    /// <summary>
    /// Gets or sets the window associated with this view model.
    /// </summary>
    public Window? Window { get; set; }

    public string Greeting { get; } = "Welcome to Avalonia!";


    [RelayCommand]
    private async Task OpenFileAsync()
    {
        if (Window == null)
            return;

        var storageProvider = Window.StorageProvider;

        var files = await storageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Open Data File",
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("All Supported Files") { Patterns = ["*.json", "*.xml", "*.yml", "*.yaml", "*.csv"] },
                new FilePickerFileType("JSON Files") { Patterns = ["*.json"] },
                new FilePickerFileType("XML Files") { Patterns = ["*.xml"] },
                new FilePickerFileType("YAML Files") { Patterns = ["*.yml", "*.yaml"] },
                new FilePickerFileType("CSV Files") { Patterns = ["*.csv"] },
                new FilePickerFileType("All Files") { Patterns = ["*"] }
            ]
        });

        if (files == null || files.Count == 0)
            return;

        var file = files[0];
        var filePath = file.Path.LocalPath;

        // Load the file into the current window
        await LoadFromFileAsync(filePath);
    }

    [RelayCommand]
    private void CloseWindow()
    {
        Window?.Close();
    }

    [RelayCommand]
    private async Task ShowHelpAsync()
    {
        if (Window == null)
            return;

        var helpText = AppViewModel.GetHelpText();
        var dialog = new Avalonia.Controls.Window
        {
            Title = "About PrettyStick",
            Width = 500,
            Height = 400,
            WindowStartupLocation = Avalonia.Controls.WindowStartupLocation.CenterOwner,
            Content = new Avalonia.Controls.ScrollViewer
            {
                Content = new Avalonia.Controls.TextBlock
                {
                    Text = helpText,
                    FontFamily = new Avalonia.Media.FontFamily("Consolas, Courier New, monospace"),
                    Margin = new Avalonia.Thickness(16),
                    TextWrapping = Avalonia.Media.TextWrapping.Wrap
                }
            }
        };

        await dialog.ShowDialog(Window);
    }

    /// <summary>
    /// Loads data from a file path with progress reporting.
    /// </summary>
    public async Task<bool> LoadFromFileAsync(string filePath, DataFormat format = DataFormat.Auto)
    {
        FilePath = filePath;
        Format = format;
        HasError = false;
        ErrorMessage = null;

        if (!File.Exists(filePath))
        {
            HasError = true;
            ErrorMessage = $"File not found: {filePath}";
            return false;
        }

        IsLoading = true;
        LoadingProgress = 0;
        LoadingStatus = "Reading file...";

        Stopwatch sw = new Stopwatch();
        sw.Start();
        try
        {
            var fileInfo = new FileInfo(filePath);
            LoadingMaximum = fileInfo.Length;

            // Read file with progress reporting
            InputData = await ReadFileWithProgressAsync(filePath, fileInfo.Length);
        }
        catch (Exception ex)
        {
            IsLoading = false;
            HasError = true;
            ErrorMessage = $"Error reading file: {ex.Message}";
            return false;
        }
        sw.Stop();
        Debug.WriteLine($"File read in {sw.ElapsedMilliseconds} ms");

        LoadingStatus = "Parsing data...";
        var result = await ParseAndDeserializeAsync();
        IsLoading = false;
        return result;
    }

    /// <summary>
    /// Reads a file with progress reporting.
    /// </summary>
    private async Task<string> ReadFileWithProgressAsync(string filePath, long totalSize)
    {
        const int BufferSize = 81920; // 80KB buffer
        using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, BufferSize, useAsync: true);
        using var reader = new StreamReader(fileStream);
        
        var result = new System.Text.StringBuilder((int)Math.Min(totalSize, int.MaxValue));
        var buffer = new char[BufferSize];
        int charsRead;
        long totalBytesRead = 0;
        var lastUpdate = DateTime.UtcNow;

        while ((charsRead = await reader.ReadAsync(buffer, 0, buffer.Length)) > 0)
        {
            result.Append(buffer, 0, charsRead);
            totalBytesRead = fileStream.Position;

            // Update progress at most every 50ms to avoid UI thrashing
            var now = DateTime.UtcNow;
            if ((now - lastUpdate).TotalMilliseconds >= 50)
            {
                LoadingProgress = totalBytesRead;
                lastUpdate = now;
            }
        }

        LoadingProgress = totalSize; // Ensure we show 100% at the end
        return result.ToString();
    }

    /// <summary>
    /// Loads data from a string.
    /// </summary>
    public async Task<bool> LoadFromStringAsync(string data, DataFormat format = DataFormat.Auto, string? filePath = null)
    {
        InputData = data;
        FilePath = filePath;
        Format = format;

        return await ParseAndDeserializeAsync();
    }

    /// <summary>
    /// Loads data from stdin.
    /// </summary>
    public async Task<bool> LoadFromStdinAsync(DataFormat format = DataFormat.Auto)
    {
        Format = format;
        HasError = false;
        ErrorMessage = null;

        if (!Console.IsInputRedirected)
        {
            return true; // No stdin data, not an error
        }

        IsLoading = true;
        LoadingProgress = 0;
        LoadingMaximum = 100; // Unknown size for stdin
        LoadingStatus = "Reading from stdin...";

        try
        {
            InputData = await Console.In.ReadToEndAsync();
        }
        catch (Exception ex)
        {
            IsLoading = false;
            HasError = true;
            ErrorMessage = $"Error reading from stdin: {ex.Message}";
            return false;
        }

        LoadingStatus = "Parsing data...";
        var result = await ParseAndDeserializeAsync();
        IsLoading = false;
        return result;
    }

    private async Task<bool> ParseAndDeserializeAsync()
    {
        Stopwatch sw = new Stopwatch();
        sw.Start();
        try
        {
            // Auto-detect format if not specified
            if (Format == DataFormat.Auto && !string.IsNullOrEmpty(InputData))
            {
                Format = DetectFormat(InputData, FilePath);
            }

            // Deserialize the input data based on format
            if (!string.IsNullOrEmpty(InputData) && Format != DataFormat.Auto)
            {
                try
                {
                    LoadingStatus = $"Parsing {Format}...";
                    LoadingProgress = 0;
                    LoadingMaximum = InputData.Length;

                    // Create progress callback that throttles UI updates
                    var lastUpdate = DateTime.UtcNow;
                    Action<long> progressCallback = (position) =>
                    {
                        var now = DateTime.UtcNow;
                        if ((now - lastUpdate).TotalMilliseconds >= 50)
                        {
                            LoadingProgress = position;
                            lastUpdate = now;
                        }
                    };

                    // Run parsing on thread pool to keep UI responsive
                    Data = await Task.Run(() => DeserializeData(InputData, Format, progressCallback));
                    LoadingProgress = InputData.Length; // Ensure 100% at end
                }
                catch (Exception ex)
                {
                    HasError = true;
                    ErrorMessage = $"Error deserializing {Format} data: {ex.Message}";
                    return false;
                }
            }

            return true;
        }
        finally
        {
            sw.Stop();
            Debug.WriteLine($"Data parsed and deserialized in {sw.ElapsedMilliseconds} ms");
        }
    }


    private static object? DeserializeData(string data, DataFormat format, Action<long>? progressCallback = null)
    {
        return format switch
        {
            DataFormat.Json => ParseJson(data, progressCallback),
            DataFormat.Xml => ParseXml(data, progressCallback),
            DataFormat.Yaml => ParseYaml(data, progressCallback),
            DataFormat.Csv => ParseCsv(data, progressCallback),
            _ => null
        };
    }

    private static JToken ParseJson(string data, Action<long>? progressCallback)
    {
        // Use JsonTextReader with LineInfo to preserve line numbers
        using var stringReader = new ProgressTextReader(data, progressCallback);
        using var jsonReader = new JsonTextReader(stringReader);
        return JToken.Load(jsonReader, new JsonLoadSettings { LineInfoHandling = LineInfoHandling.Load });
    }

    private static XDocument ParseXml(string data, Action<long>? progressCallback)
    {
        // XDocument.Parse doesn't support streaming, but we can use XmlReader for progress
        using var stringReader = new ProgressTextReader(data, progressCallback);
        using var xmlReader = System.Xml.XmlReader.Create(stringReader, new System.Xml.XmlReaderSettings { DtdProcessing = System.Xml.DtdProcessing.Ignore });
        return XDocument.Load(xmlReader, LoadOptions.SetLineInfo);
    }

    private static YamlStream ParseYaml(string data, Action<long>? progressCallback)
    {
        var yaml = new YamlStream();
        using var reader = new ProgressTextReader(data, progressCallback);
        yaml.Load(reader);
        return yaml;
    }

    private static List<dynamic> ParseCsv(string data, Action<long>? progressCallback)
    {
        using var reader = new ProgressTextReader(data, progressCallback);
        using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);
        return [.. csv.GetRecords<dynamic>()];
    }

    /// <summary>
    /// A TextReader wrapper that reports read progress via a callback.
    /// </summary>
    private sealed class ProgressTextReader : TextReader
    {
        private readonly string _data;
        private readonly Action<long>? _progressCallback;
        private int _position;

        public ProgressTextReader(string data, Action<long>? progressCallback)
        {
            _data = data;
            _progressCallback = progressCallback;
            _position = 0;
        }

        public override int Read()
        {
            if (_position >= _data.Length)
                return -1;

            var ch = _data[_position++];
            _progressCallback?.Invoke(_position);
            return ch;
        }

        public override int Read(char[] buffer, int index, int count)
        {
            if (_position >= _data.Length)
                return 0;

            int charsToRead = Math.Min(count, _data.Length - _position);
            _data.CopyTo(_position, buffer, index, charsToRead);
            _position += charsToRead;
            _progressCallback?.Invoke(_position);
            return charsToRead;
        }

        public override int Peek()
        {
            if (_position >= _data.Length)
                return -1;
            return _data[_position];
        }

        public override string? ReadLine()
        {
            if (_position >= _data.Length)
                return null;

            int start = _position;
            while (_position < _data.Length)
            {
                char ch = _data[_position];
                if (ch == '\r' || ch == '\n')
                {
                    string line = _data.Substring(start, _position - start);
                    _position++;
                    if (ch == '\r' && _position < _data.Length && _data[_position] == '\n')
                        _position++;
                    _progressCallback?.Invoke(_position);
                    return line;
                }
                _position++;
            }

            _progressCallback?.Invoke(_position);
            return _data.Substring(start);
        }

        public override string ReadToEnd()
        {
            if (_position >= _data.Length)
                return string.Empty;

            string result = _data.Substring(_position);
            _position = _data.Length;
            _progressCallback?.Invoke(_position);
            return result;
        }
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
