using System;
using System.Collections;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Reflection;
using System.Xml;
using System.Xml.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using Humanizer;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using YamlDotNet.RepresentationModel;

namespace DumpViewer;

/// <summary>
/// Represents a node in the object visualization tree.
/// </summary>
public partial class ObjectNode : ObservableObject
{
    [ObservableProperty]
    private bool _isExpanded;

    /// <summary>
    /// Gets the name/key of this node (property name, index, dictionary key, etc.).
    /// </summary>
    public string? Name { get; }

    /// <summary>
    /// Gets the type of the value.
    /// </summary>
    public Type? ValueType { get; }

    /// <summary>
    /// Gets the original value being visualized.
    /// </summary>
    public object? Value { get; }

    /// <summary>
    /// Gets the display string for the value.
    /// </summary>
    public string DisplayValue { get; }

    /// <summary>
    /// Gets the type name to display.
    /// </summary>
    public string TypeName { get; }

    /// <summary>
    /// Gets the kind of node for styling purposes.
    /// </summary>
    public NodeKind Kind { get; }

    /// <summary>
    /// Gets whether this node has children that can be expanded.
    /// </summary>
    public bool HasChildren { get; }

    /// <summary>
    /// Gets whether this node is expandable (has children and is not a primitive).
    /// </summary>
    public bool IsExpandable => HasChildren;

    /// <summary>
    /// Gets the start line number in the source text (1-based), or null if not available.
    /// </summary>
    public int? StartLine { get; }

    /// <summary>
    /// Gets the start column in the source text (1-based), or null if not available.
    /// </summary>
    public int? StartColumn { get; }

    /// <summary>
    /// Gets the end line number in the source text (1-based), or null if not available.
    /// </summary>
    public int? EndLine { get; }

    /// <summary>
    /// Gets the end column in the source text (1-based), or null if not available.
    /// </summary>
    public int? EndColumn { get; }

    /// <summary>
    /// Gets whether this node has source location information.
    /// </summary>
    public bool HasSourceLocation => StartLine.HasValue;

    /// <summary>
    /// Gets a comma-delimited preview of the first 5 values for Object nodes.
    /// Only provides preview if children are already loaded to avoid triggering expensive child creation.
    /// </summary>
    public string? ValuesPreview
    {
        get
        {
            if (Kind != NodeKind.Object || !HasChildren)
                return null;

            // Don't trigger child creation just for preview - only show if already loaded
            if (_children == null)
                return null;

            var previewValues = _children
                .Take(5)
                .Select(c => c.DisplayValue switch
                {
                    "" => c.TypeName,
                    _ => c.DisplayValue
                })
                .Where(v => !string.IsNullOrEmpty(v));

            var preview = string.Join(", ", previewValues);
            return string.IsNullOrEmpty(preview) ? null : preview;
        }
    }

    /// <summary>
    /// Gets the child nodes (lazily populated).
    /// </summary>
    public IReadOnlyList<ObjectNode> Children => _children ??= CreateChildren();

    /// <summary>
    /// Gets the count of children without materializing them if possible.
    /// Falls back to accessing Children.Count if no optimized count is available.
    /// </summary>
    public int ChildCount
    {
        get
        {
            // If children are already created, use their count
            if (_children != null)
                return _children.Count;

            // Try to get count from underlying value without creating ObjectNode children
            var fastCount = GetChildCountFromValue();
            if (fastCount.HasValue)
                return fastCount.Value;
            
            // For unknown types, return 0 rather than triggering full Children creation
            // This is safer for virtualization - we'll create children when actually accessed
            return HasChildren ? 1 : 0; // Assume at least 1 if HasChildren is true
        }
    }

    /// <summary>
    /// Gets the child count directly from the underlying value if possible.
    /// Returns null if we need to fall back to creating children.
    /// </summary>
    private int? GetChildCountFromValue()
    {
        if (Value == null || !HasChildren)
            return 0;

        // JSON
        if (Value is JObject jObj) return jObj.Count;
        if (Value is JArray jArr) return jArr.Count;

        // YAML
        if (Value is YamlMappingNode yamlMap) return yamlMap.Children.Count;
        if (Value is YamlSequenceNode yamlSeq) return yamlSeq.Children.Count;
        if (Value is YamlStream yamlStream) return yamlStream.Documents.Count;
        if (Value is YamlDocument yamlDoc) return yamlDoc.RootNode != null ? 1 : 0;

        // XML
        if (Value is XDocument xDoc) return xDoc.Root != null ? 1 : 0;
        if (Value is XElement xElement)
        {
            int count = xElement.Attributes().Count();
            count += xElement.Elements().Count();
            return count;
        }

        // ExpandoObject / dynamic
        if (Value is System.Dynamic.ExpandoObject expando)
            return ((IDictionary<string, object?>)expando).Count;
        if (Value is IDictionary<string, object> dynamicDict && IsDynamicObject(Value.GetType()))
            return dynamicDict.Count;

        // Generic collections - try to get count without full enumeration
        if (Value is System.Collections.ICollection collection)
            return collection.Count;

        // Fall back to null - will create children
        return null;
    }

    private IReadOnlyList<ObjectNode>? _children;
    private readonly int _maxDepth;
    private readonly int _currentDepth;
    private readonly HashSet<object> _visited;
    private readonly string? _inferredItemTypeName;

    public ObjectNode(object? value, string? name = null, int maxDepth = 10, int currentDepth = 0, HashSet<object>? visited = null, string? inferredItemTypeName = null, int? overrideStartLine = null)
    {
        _maxDepth = maxDepth;
        _currentDepth = currentDepth;
        _visited = visited ?? new HashSet<object>(ReferenceEqualityComparer.Instance);
        _inferredItemTypeName = inferredItemTypeName;

        Name = name;
        Value = value;
        ValueType = value?.GetType();

        (Kind, DisplayValue, TypeName, HasChildren) = AnalyzeValue(value);
        
        // Use override line if provided (e.g., for CSV rows), otherwise extract from value
        if (overrideStartLine.HasValue)
        {
            StartLine = overrideStartLine.Value;
            StartColumn = 1;
            EndLine = overrideStartLine.Value;
            EndColumn = null;
        }
        else
        {
            (StartLine, StartColumn, EndLine, EndColumn) = ExtractSourceLocation(value);
        }

        // Auto-expand first level, but not if it has too many children (performance)
        // Large collections should be manually expanded
        const int AutoExpandThreshold = 100;
        IsExpanded = currentDepth == 0 && (!HasChildren || ChildCount <= AutoExpandThreshold);
    }




    private static (int? startLine, int? startColumn, int? endLine, int? endColumn) ExtractSourceLocation(object? value)
    {
        // JSON tokens (Newtonsoft.Json) implement IJsonLineInfo
        if (value is JToken jToken && jToken is IJsonLineInfo jsonLineInfo && jsonLineInfo.HasLineInfo())
        {
            return (jsonLineInfo.LineNumber, jsonLineInfo.LinePosition, null, null);
        }
        
        // YAML nodes have Start and End marks
        if (value is YamlNode yamlNode)
        {
            return ((int)yamlNode.Start.Line, (int)yamlNode.Start.Column, (int)yamlNode.End.Line, (int)yamlNode.End.Column);
        }
        
        // LINQ to XML nodes (XDocument, XElement, XAttribute) implement IXmlLineInfo
        if (value is XObject xObject && xObject is IXmlLineInfo lineInfo && lineInfo.HasLineInfo())
        {
            return (lineInfo.LineNumber, lineInfo.LinePosition, null, null);
        }
        
        return (null, null, null, null);
    }

    private (NodeKind kind, string displayValue, string typeName, bool hasChildren) AnalyzeValue(object? value)
    {
        if (value == null)
        {
            return (NodeKind.Null, "null", "", false);
        }

        var type = value.GetType();
        var typeName = GetFriendlyTypeName(type);

        // Check for circular reference
        if (!type.IsValueType && _visited.Contains(value))
        {
            return (NodeKind.CircularReference, "(circular reference)", typeName, false);
        }

        // Check max depth
        if (_currentDepth >= _maxDepth)
        {
            return (NodeKind.MaxDepth, "(max depth reached)", typeName, false);
        }

        // === Special data format handling ===

        // JSON tokens (Newtonsoft.Json)
        if (value is JValue jValue)
        {
            return AnalyzeJValue(jValue);
        }
        if (value is JObject jObj)
        {
            var objTypeName = _inferredItemTypeName ?? "Object";
            return (NodeKind.Object, "", objTypeName, jObj.Count > 0);
        }
        if (value is JArray jArr)
        {
            return (NodeKind.Collection, $"({jArr.Count} items)", "Array", jArr.Count > 0);
        }

        // YAML nodes
        if (value is YamlScalarNode yamlScalar)
        {
            return AnalyzeYamlScalar(yamlScalar);
        }
        if (value is YamlMappingNode yamlMap)
        {
            var objTypeName = _inferredItemTypeName ?? "Object";
            return (NodeKind.Object, "", objTypeName, yamlMap.Children.Count > 0);
        }
        if (value is YamlSequenceNode yamlSeq)
        {
            return (NodeKind.Collection, $"({yamlSeq.Children.Count} items)", "Array", yamlSeq.Children.Count > 0);
        }
        if (value is YamlStream yamlStream)
        {
            return (NodeKind.Collection, $"({yamlStream.Documents.Count} documents)", "YamlStream", yamlStream.Documents.Count > 0);
        }
        if (value is YamlDocument yamlDoc)
        {
            return (NodeKind.Object, "", "Document", yamlDoc.RootNode != null);
        }

        // XML nodes (LINQ to XML)
        if (value is XDocument xDoc)
        {
            return (NodeKind.Object, "", "XDocument", xDoc.Root != null);
        }
        if (value is XElement xElement)
        {
            var childElements = xElement.Elements().ToList();
            var attrCount = xElement.Attributes().Count();
            var textContent = GetXElementTextContent(xElement);

            // If element has only text content (no child elements, no attributes), treat as a leaf with the text value
            if (childElements.Count == 0 && attrCount == 0)
            {
                if (string.IsNullOrEmpty(textContent))
                    return (NodeKind.Null, "null", "", false);
                return (NodeKind.String, $"\"{EscapeString(textContent)}\"", "string", false);
            }

            // Check if all child elements have the same name (it's an array container)
            var distinctNames = childElements.Select(e => e.Name.LocalName).Distinct().ToList();
            if (distinctNames.Count == 1 && childElements.Count > 1)
            {
                return (NodeKind.Collection, $"({childElements.Count} items)", "Array", true);
            }

            // Has children or attributes - it's a complex object
            var objTypeName = _inferredItemTypeName ?? xElement.Name.LocalName;
            return (NodeKind.Object, "", objTypeName, true);
        }
        if (value is XAttribute xAttr)
        {
            // Attributes are always string values
            return (NodeKind.String, $"\"{EscapeString(xAttr.Value)}\"", "string", false);
        }

        // ExpandoObject or dynamic objects (from CsvHelper dynamic records)
        // FastDynamicObject and similar implement IDictionary<string, object>
        if (value is ExpandoObject expando)
        {
            var expandoDict = (IDictionary<string, object?>)expando;
            var objTypeName = _inferredItemTypeName ?? "Row";
            return (NodeKind.Object, "", objTypeName, expandoDict.Count > 0);
        }
        if (value is IDictionary<string, object> dynamicDict && IsDynamicObject(type))
        {
            var objTypeName = _inferredItemTypeName ?? "Row";
            return (NodeKind.Object, "", objTypeName, dynamicDict.Count > 0);
        }

        // === Standard type handling ===

        // Primitives and simple types
        if (IsPrimitive(type))
        {
            return (NodeKind.Primitive, FormatPrimitive(value), typeName, false);
        }

        // Strings
        if (value is string str)
        {
            return (NodeKind.String, $"\"{EscapeString(str)}\"", "string", false);
        }

        // Enums
        if (type.IsEnum)
        {
            return (NodeKind.Enum, value.ToString() ?? "", typeName, false);
        }

        // DateTime
        if (value is DateTime dt)
        {
            return (NodeKind.DateTime, dt.ToString("O"), "DateTime", false);
        }

        // DateTimeOffset
        if (value is DateTimeOffset dto)
        {
            return (NodeKind.DateTime, dto.ToString("O"), "DateTimeOffset", false);
        }

        // TimeSpan
        if (value is TimeSpan ts)
        {
            return (NodeKind.TimeSpan, ts.ToString(), "TimeSpan", false);
        }

        // Guid
        if (value is Guid guid)
        {
            return (NodeKind.Guid, guid.ToString(), "Guid", false);
        }

        // Dictionaries
        if (value is IDictionary dict)
        {
            return (NodeKind.Dictionary, $"({dict.Count} items)", typeName, dict.Count > 0);
        }

        // Collections/Arrays
        if (value is IEnumerable enumerable and not string)
        {
            var items = enumerable.Cast<object?>().ToList();
            var count = items.Count;
            
            // Check if this is a collection of dynamic objects (CSV data)
            if (count > 0 && items[0] != null)
            {
                var firstItem = items[0]!;
                var firstType = firstItem.GetType();
                if (firstItem is ExpandoObject || (firstItem is IDictionary<string, object> && IsDynamicObject(firstType)))
                {
                    return (NodeKind.Collection, $"({count} items)", "Array", count > 0);
                }
            }
            
            return (NodeKind.Collection, $"({count} items)", typeName, count > 0);
        }

        // Complex objects
        var properties = GetVisibleProperties(type);
        return (NodeKind.Object, "", typeName, properties.Length > 0);
    }

    private static (NodeKind kind, string displayValue, string typeName, bool hasChildren) AnalyzeJValue(JValue jValue)
    {
        return jValue.Type switch
        {
            JTokenType.String => (NodeKind.String, $"\"{EscapeString(jValue.Value?.ToString() ?? "")}\"", "string", false),
            JTokenType.Integer => (NodeKind.Primitive, jValue.Value?.ToString() ?? "0", "number", false),
            JTokenType.Float => (NodeKind.Primitive, jValue.Value?.ToString() ?? "0", "number", false),
            JTokenType.Boolean => (NodeKind.Primitive, jValue.Value?.ToString()?.ToLowerInvariant() ?? "false", "boolean", false),
            JTokenType.Null => (NodeKind.Null, "null", "", false),
            JTokenType.Date => (NodeKind.DateTime, jValue.Value?.ToString() ?? "", "DateTime", false),
            JTokenType.Guid => (NodeKind.Guid, jValue.Value?.ToString() ?? "", "Guid", false),
            JTokenType.TimeSpan => (NodeKind.TimeSpan, jValue.Value?.ToString() ?? "", "TimeSpan", false),
            JTokenType.Uri => (NodeKind.String, $"\"{EscapeString(jValue.Value?.ToString() ?? "")}\"", "Uri", false),
            _ => (NodeKind.String, jValue.Value?.ToString() ?? "", "unknown", false)
        };
    }

    private static (NodeKind kind, string displayValue, string typeName, bool hasChildren) AnalyzeYamlScalar(YamlScalarNode scalar)
    {
        var value = scalar.Value ?? "";
        
        // Try to infer type from YAML scalar style and content
        if (scalar.Style == YamlDotNet.Core.ScalarStyle.Plain)
        {
            if (value is "true" or "false")
                return (NodeKind.Primitive, value, "boolean", false);
            if (value == "null" || value == "~" || string.IsNullOrEmpty(value))
                return (NodeKind.Null, "null", "", false);
            if (double.TryParse(value, out _))
                return (NodeKind.Primitive, value, "number", false);
        }
        
        return (NodeKind.String, $"\"{EscapeString(value)}\"", "string", false);
    }

    private IReadOnlyList<ObjectNode> CreateChildren()
    {
        if (!HasChildren || Value == null)
        {
            return Array.Empty<ObjectNode>();
        }

        var type = Value.GetType();


        // Track this object to detect circular references
        var newVisited = new HashSet<object>(_visited, ReferenceEqualityComparer.Instance);
        if (!type.IsValueType)
        {
            newVisited.Add(Value);
        }

        var children = new List<ObjectNode>();

        // === Special data format handling ===

        // JSON Object (Newtonsoft.Json)
        if (Value is JObject jObj)
        {
            foreach (var prop in jObj.Properties().OrderBy(p => p.Name))
            {
                // Infer item type name if the value is an array
                string? itemTypeName = prop.Value is JArray ? prop.Name.Singularize() : null;
                children.Add(new ObjectNode(prop.Value, prop.Name, _maxDepth, _currentDepth + 1, newVisited, itemTypeName));
            }
            return children;
        }

        // JSON Array (Newtonsoft.Json)
        if (Value is JArray jArr)
        {
            int index = 0;
            foreach (var item in jArr)
            {
                children.Add(new ObjectNode(item, $"[{index}]", _maxDepth, _currentDepth + 1, newVisited, _inferredItemTypeName));
                index++;
            }
            return children;
        }

        // YAML Mapping
        if (Value is YamlMappingNode yamlMap)
        {
            foreach (var entry in yamlMap.Children.OrderBy(e => e.Key.ToString()))
            {
                var key = (entry.Key as YamlScalarNode)?.Value ?? entry.Key.ToString();
                string? itemTypeName = entry.Value is YamlSequenceNode ? key.Singularize() : null;
                children.Add(new ObjectNode(entry.Value, key, _maxDepth, _currentDepth + 1, newVisited, itemTypeName));
            }
            return children;
        }

        // YAML Sequence
        if (Value is YamlSequenceNode yamlSeq)
        {
            int index = 0;
            foreach (var item in yamlSeq.Children)
            {
                children.Add(new ObjectNode(item, $"[{index}]", _maxDepth, _currentDepth + 1, newVisited, _inferredItemTypeName));
                index++;
            }
            return children;
        }

        // YAML Stream
        if (Value is YamlStream yamlStream)
        {
            int index = 0;
            foreach (var doc in yamlStream.Documents)
            {
                children.Add(new ObjectNode(doc.RootNode, $"Document[{index}]", _maxDepth, _currentDepth + 1, newVisited));
                index++;
            }
            return children;
        }

        // YAML Document
        if (Value is YamlDocument yamlDoc && yamlDoc.RootNode != null)
        {
            children.Add(new ObjectNode(yamlDoc.RootNode, "Root", _maxDepth, _currentDepth + 1, newVisited));
            return children;
        }

        // XML Document (LINQ to XML)
        if (Value is XDocument xDoc && xDoc.Root != null)
        {
            children.Add(new ObjectNode(xDoc.Root, xDoc.Root.Name.LocalName, _maxDepth, _currentDepth + 1, newVisited));
            return children;
        }

        // XML Element (LINQ to XML)
        if (Value is XElement xElement)
        {
            // Add attributes first
            foreach (var attr in xElement.Attributes())
            {
                children.Add(new ObjectNode(attr, $"@{attr.Name.LocalName}", _maxDepth, _currentDepth + 1, newVisited));
            }

            // Group child elements by name to detect arrays
            var childElements = xElement.Elements().ToList();

            // Check if all child elements have the same name (it's an array)
            var distinctNames = childElements.Select(e => e.Name.LocalName).Distinct().ToList();
            
            if (distinctNames.Count == 1 && childElements.Count > 0)
            {
                // All children have the same name - treat as array with indexes
                var itemTypeName = distinctNames[0].Singularize();
                int index = 0;
                foreach (var childElement in childElements)
                {
                    children.Add(new ObjectNode(childElement, $"[{index}]", _maxDepth, _currentDepth + 1, newVisited, itemTypeName));
                    index++;
                }
            }
            else
            {
                // Mixed children - use element names
                foreach (var childElement in childElements)
                {
                    children.Add(new ObjectNode(childElement, childElement.Name.LocalName, _maxDepth, _currentDepth + 1, newVisited));
                }
            }
            return children;
        }

        // ExpandoObject (from CsvHelper)
        if (Value is ExpandoObject expandoObj)
        {
            var expandoDict = (IDictionary<string, object?>)expandoObj;
            foreach (var kvp in expandoDict.OrderBy(k => k.Key))
            {
                children.Add(new ObjectNode(kvp.Value, kvp.Key, _maxDepth, _currentDepth + 1, newVisited));
            }
            return children;
        }


        // Dynamic objects like FastDynamicObject (from CsvHelper)
        if (Value is IDictionary<string, object> dynamicDict && IsDynamicObject(type))
        {
            foreach (var kvp in dynamicDict.OrderBy(k => k.Key))
            {
                children.Add(new ObjectNode(kvp.Value, kvp.Key, _maxDepth, _currentDepth + 1, newVisited));
            }
            return children;
        }

        // === Standard type handling ===


        // Dictionary
        if (Value is IDictionary dict)
        {
            foreach (DictionaryEntry entry in dict)
            {
                var keyStr = entry.Key?.ToString() ?? "null";
                children.Add(new ObjectNode(entry.Value, keyStr, _maxDepth, _currentDepth + 1, newVisited));
            }
            return children;
        }

        // Collection/Array
        if (Value is IEnumerable enumerable and not string)
        {
            int index = 0;
            foreach (var item in enumerable)
            {
                // For dynamic objects (CSV rows), infer "Row" as the type name and calculate line number
                var itemTypeName = _inferredItemTypeName;
                int? csvLineNumber = null;
                
                if (item != null)
                {
                    var itemType = item.GetType();
                    if (item is ExpandoObject || (item is IDictionary<string, object> && IsDynamicObject(itemType)))
                    {
                        if (itemTypeName == null) itemTypeName = "Row";
                        // CSV line = index + 2 (1 for header row, 1 for 1-based line numbers)
                        csvLineNumber = index + 2;
                    }
                }
                children.Add(new ObjectNode(item, $"[{index}]", _maxDepth, _currentDepth + 1, newVisited, itemTypeName, csvLineNumber));
                index++;
            }
            return children;
        }

        // Complex object - show properties
        var properties = GetVisibleProperties(type);
        foreach (var prop in properties)
        {
            try
            {
                var propValue = prop.GetValue(Value);
                children.Add(new ObjectNode(propValue, prop.Name, _maxDepth, _currentDepth + 1, newVisited));
            }
            catch (Exception ex)
            {
                children.Add(new ObjectNode($"<error: {ex.Message}>", prop.Name, _maxDepth, _currentDepth + 1, newVisited));
            }
        }

        return children;
    }

    private static PropertyInfo[] GetVisibleProperties(Type type)
    {
        return type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanRead && p.GetIndexParameters().Length == 0)
            .OrderBy(p => p.Name)
            .ToArray();
    }

    private static bool IsPrimitive(Type type)
    {
        return type.IsPrimitive || 
               type == typeof(decimal) || 
               type == typeof(byte[]);
    }

    /// <summary>
    /// Checks if a type is a dynamic object (like FastDynamicObject from CsvHelper).
    /// </summary>
    private static bool IsDynamicObject(Type type)
    {
        // Check for common dynamic object types
        var typeName = type.Name;
        if (typeName.Contains("Dynamic", StringComparison.OrdinalIgnoreCase))
            return true;
        
        // Check if it inherits from DynamicObject
        if (typeof(DynamicObject).IsAssignableFrom(type))
            return true;

        return false;
    }

    private static string FormatPrimitive(object value)
    {
        return value switch
        {
            bool b => b ? "true" : "false",
            byte[] bytes => $"byte[{bytes.Length}]",
            float f => f.ToString("G9"),
            double d => d.ToString("G17"),
            decimal m => m.ToString("G"),
            _ => value.ToString() ?? ""
        };
    }

    private static string EscapeString(string str)
    {
        if (str.Length > 1000)
        {
            str = str[..1000] + "...";
        }
        return str.Replace("\\", "\\\\")
                  .Replace("\"", "\\\"")
                  .Replace("\n", "\\n")
                  .Replace("\r", "\\r")
                  .Replace("\t", "\\t");
    }

    private static string GetXElementTextContent(XElement element)
    {
        // Get direct text content, excluding child elements
        return string.Join("", element.Nodes()
            .Where(n => n is XText)
            .Cast<XText>()
            .Select(t => t.Value?.Trim() ?? ""))
            .Trim();
    }

    private static string GetFriendlyTypeName(Type type)
    {
        if (type.IsGenericType)
        {
            var genericDef = type.GetGenericTypeDefinition();
            var baseName = genericDef.Name;
            var tickIndex = baseName.IndexOf('`');
            if (tickIndex > 0)
            {
                baseName = baseName[..tickIndex];
            }

            var typeArgs = string.Join(", ", type.GetGenericArguments().Select(GetFriendlyTypeName));
            return $"{baseName}<{typeArgs}>";
        }

        if (type.IsArray)
        {
            return $"{GetFriendlyTypeName(type.GetElementType()!)}[]";
        }

        return type.Name;
    }

}

/// <summary>
/// The kind of node for styling purposes.
/// </summary>
public enum NodeKind
{
    Null,
    Primitive,
    String,
    Enum,
    DateTime,
    TimeSpan,
    Guid,
    Collection,
    Dictionary,
    Object,
    CircularReference,
    MaxDepth
}

/// <summary>
/// Reference equality comparer for cycle detection.
/// </summary>
internal sealed class ReferenceEqualityComparer : IEqualityComparer<object>
{
    public static ReferenceEqualityComparer Instance { get; } = new();

    private ReferenceEqualityComparer() { }

    public new bool Equals(object? x, object? y) => ReferenceEquals(x, y);
    public int GetHashCode(object obj) => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
}
