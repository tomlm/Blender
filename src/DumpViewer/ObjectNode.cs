using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using CommunityToolkit.Mvvm.ComponentModel;

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
    /// Gets the child nodes (lazily populated).
    /// </summary>
    public IReadOnlyList<ObjectNode> Children => _children ??= CreateChildren();

    private IReadOnlyList<ObjectNode>? _children;
    private readonly int _maxDepth;
    private readonly int _currentDepth;
    private readonly HashSet<object> _visited;

    public ObjectNode(object? value, string? name = null, int maxDepth = 10, int currentDepth = 0, HashSet<object>? visited = null)
    {
        _maxDepth = maxDepth;
        _currentDepth = currentDepth;
        _visited = visited ?? new HashSet<object>(ReferenceEqualityComparer.Instance);

        Name = name;
        Value = value;
        ValueType = value?.GetType();

        (Kind, DisplayValue, TypeName, HasChildren) = AnalyzeValue(value);

        // Auto-expand first level
        IsExpanded = currentDepth == 0;
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
            var count = enumerable.Cast<object?>().Count();
            return (NodeKind.Collection, $"({count} items)", typeName, count > 0);
        }

        // Complex objects
        var properties = GetVisibleProperties(type);
        return (NodeKind.Object, "", typeName, properties.Length > 0);
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

        // Dictionary
        if (Value is IDictionary dict)
        {
            foreach (DictionaryEntry entry in dict)
            {
                var keyStr = entry.Key?.ToString() ?? "null";
                children.Add(new ObjectNode(entry.Value, $"[{keyStr}]", _maxDepth, _currentDepth + 1, newVisited));
            }
            return children;
        }

        // Collection/Array
        if (Value is IEnumerable enumerable and not string)
        {
            int index = 0;
            foreach (var item in enumerable)
            {
                children.Add(new ObjectNode(item, $"[{index}]", _maxDepth, _currentDepth + 1, newVisited));
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
