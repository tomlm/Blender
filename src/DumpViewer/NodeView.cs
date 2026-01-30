using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Data;

namespace DumpViewer;

/// <summary>
/// A control that displays a single node in the object visualization tree.
/// </summary>
public class NodeView : TemplatedControl
{
    /// <summary>
    /// Defines the <see cref="Node"/> property.
    /// </summary>
    public static readonly StyledProperty<ObjectNode?> NodeProperty =
        AvaloniaProperty.Register<NodeView, ObjectNode?>(nameof(Node));

    /// <summary>
    /// Gets or sets the node to display.
    /// </summary>
    public ObjectNode? Node
    {
        get => GetValue(NodeProperty);
        set => SetValue(NodeProperty, value);
    }
}
