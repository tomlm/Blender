using System;
using System.IO;
using System.Reflection;
using System.Xml;
using AvaloniaEdit.Highlighting;
using AvaloniaEdit.Highlighting.Xshd;

namespace DumpViewer;

/// <summary>
/// Manages syntax highlighting definitions for the source text viewer.
/// </summary>
public static class SyntaxHighlightingManager
{
    private static bool _initialized;
    
    /// <summary>
    /// Initializes the syntax highlighting definitions from embedded resources.
    /// </summary>
    public static void Initialize()
    {
        if (_initialized)
            return;
            
        _initialized = true;
        
        var assembly = typeof(SyntaxHighlightingManager).Assembly;
        
        RegisterHighlighting("JSON", [".json"], assembly, "DumpViewer.SyntaxHighlighting.JSON.xshd");
        RegisterHighlighting("YAML", [".yaml", ".yml"], assembly, "DumpViewer.SyntaxHighlighting.YAML.xshd");
        RegisterHighlighting("XML", [".xml"], assembly, "DumpViewer.SyntaxHighlighting.XML.xshd");
        RegisterHighlighting("CSV", [".csv"], assembly, "DumpViewer.SyntaxHighlighting.CSV.xshd");
    }
    
    private static void RegisterHighlighting(string name, string[] extensions, Assembly assembly, string resourceName)
    {
        try
        {
            using var stream = assembly.GetManifestResourceStream(resourceName);
            if (stream == null)
                return;
                
            using var reader = new XmlTextReader(stream);
            var definition = HighlightingLoader.Load(reader, HighlightingManager.Instance);
            
            HighlightingManager.Instance.RegisterHighlighting(name, extensions, definition);
        }
        catch
        {
            // Silently ignore if highlighting can't be loaded
        }
    }
    
    /// <summary>
    /// Gets the appropriate highlighting definition for a format name (json, yaml, xml, csv).
    /// </summary>
    public static IHighlightingDefinition? GetHighlightingForFormat(string? formatName)
    {
        Initialize();
        
        return formatName?.ToLowerInvariant() switch
        {
            "json" => HighlightingManager.Instance.GetDefinition("Json"),
            "yaml" or "yml" => HighlightingManager.Instance.GetDefinition("Yaml"),
            "xml" => HighlightingManager.Instance.GetDefinition("XML"),
            "csv" => HighlightingManager.Instance.GetDefinition("Csv"),
            _ => null
        };
    }
    
    /// <summary>
    /// Gets the appropriate highlighting definition by name.
    /// </summary>
    public static IHighlightingDefinition? GetHighlighting(string name)
    {
        Initialize();
        return HighlightingManager.Instance.GetDefinition(name);
    }
}
