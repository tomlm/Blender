using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace DumpViewer;

/// <summary>
/// Converts NodeKind to a brush color for syntax highlighting.
/// </summary>
public class NodeKindToBrushConverter : IValueConverter
{
    private static readonly Dictionary<NodeKind, IBrush> KindBrushes = new()
    {
        { NodeKind.Null, new SolidColorBrush(Color.Parse("#569CD6")) },
        { NodeKind.String, new SolidColorBrush(Color.Parse("#CE9178")) },
        { NodeKind.Primitive, new SolidColorBrush(Color.Parse("#B5CEA8")) },
        { NodeKind.Enum, new SolidColorBrush(Color.Parse("#4EC9B0")) },
        { NodeKind.DateTime, new SolidColorBrush(Color.Parse("#DCDCAA")) },
        { NodeKind.TimeSpan, new SolidColorBrush(Color.Parse("#DCDCAA")) },
        { NodeKind.Guid, new SolidColorBrush(Color.Parse("#DCDCAA")) },
        { NodeKind.Collection, new SolidColorBrush(Color.Parse("#808080")) },
        { NodeKind.Dictionary, new SolidColorBrush(Color.Parse("#808080")) },
        { NodeKind.Object, new SolidColorBrush(Color.Parse("#D4D4D4")) },
        { NodeKind.CircularReference, new SolidColorBrush(Color.Parse("#808080")) },
        { NodeKind.MaxDepth, new SolidColorBrush(Color.Parse("#808080")) },
    };

    private static readonly IBrush DefaultBrush = new SolidColorBrush(Color.Parse("#D4D4D4"));

    public static NodeKindToBrushConverter Instance { get; } = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is NodeKind kind && KindBrushes.TryGetValue(kind, out var brush))
        {
            return brush;
        }
        return DefaultBrush;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converts NodeKind to font style (italic for null/special values).
/// </summary>
public class NodeKindToFontStyleConverter : IValueConverter
{
    private static readonly HashSet<NodeKind> ItalicKinds =
    [
        NodeKind.Null,
        NodeKind.CircularReference,
        NodeKind.MaxDepth
    ];

    public static NodeKindToFontStyleConverter Instance { get; } = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is NodeKind kind && ItalicKinds.Contains(kind))
        {
            return FontStyle.Italic;
        }
        return FontStyle.Normal;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
