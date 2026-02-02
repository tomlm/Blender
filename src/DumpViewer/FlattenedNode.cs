using System;

namespace DumpViewer;

/// <summary>
/// Represents a node in the flattened virtualized view.
/// Wraps an ObjectNode with additional display state.
/// </summary>
public class FlattenedNode
{
    /// <summary>
    /// Gets the underlying object node.
    /// </summary>
    public ObjectNode Node { get; }

    /// <summary>
    /// Gets the depth level for indentation (0 = root).
    /// </summary>
    public int Depth { get; }

    /// <summary>
    /// Gets the indentation width in pixels (Depth * IndentSize).
    /// </summary>
    public double IndentWidth => Depth * 16;

    /// <summary>
    /// Gets whether this node can be expanded (has children).
    /// </summary>
    public bool CanExpand => Node.HasChildren;

    /// <summary>
    /// Gets or sets whether this node is expanded.
    /// </summary>
    public bool IsExpanded
    {
        get => Node.IsExpanded;
        set => Node.IsExpanded = value;
    }

    /// <summary>
    /// Gets the expand/collapse icon based on state.
    /// </summary>
    public string ExpandIcon => !CanExpand ? "   " : (IsExpanded ? "- " : "+ ");

    // Forward properties from ObjectNode for binding
    public string? Name => Node.Name;
    public string DisplayValue => Node.DisplayValue;
    public string TypeName => Node.TypeName;
    public NodeKind Kind => Node.Kind;
    public bool HasChildren => Node.HasChildren;
    public int? StartLine => Node.StartLine;
    public bool HasSourceLocation => Node.HasSourceLocation;
    public string? ValuesPreview => Node.ValuesPreview;

    public FlattenedNode(ObjectNode node, int depth)
    {
        Node = node ?? throw new ArgumentNullException(nameof(node));
        Depth = depth;
    }
}
