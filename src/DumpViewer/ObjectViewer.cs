using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;

namespace DumpViewer;

/// <summary>
/// A control that visualizes any object in a tree structure similar to LinqPad's .Dump().
/// </summary>
public class ObjectViewer : TemplatedControl
{
    /// <summary>
    /// Defines the <see cref="Value"/> property.
    /// </summary>
    public static readonly StyledProperty<object?> ValueProperty =
        AvaloniaProperty.Register<ObjectViewer, object?>(nameof(Value));

    /// <summary>
    /// Defines the <see cref="MaxDepth"/> property.
    /// </summary>
    public static readonly StyledProperty<int> MaxDepthProperty =
        AvaloniaProperty.Register<ObjectViewer, int>(nameof(MaxDepth), 10);

    /// <summary>
    /// Defines the <see cref="RootNode"/> property.
    /// </summary>
    public static readonly DirectProperty<ObjectViewer, ObjectNode?> RootNodeProperty =
        AvaloniaProperty.RegisterDirect<ObjectViewer, ObjectNode?>(
            nameof(RootNode),
            o => o.RootNode);

    private ObjectNode? _rootNode;

    /// <summary>
    /// Gets or sets the object to visualize.
    /// </summary>
    public object? Value
    {
        get => GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    /// <summary>
    /// Gets or sets the maximum depth for recursive visualization.
    /// </summary>
    public int MaxDepth
    {
        get => GetValue(MaxDepthProperty);
        set => SetValue(MaxDepthProperty, value);
    }

    /// <summary>
    /// Gets the root node of the visualization tree.
    /// </summary>
    public ObjectNode? RootNode
    {
        get => _rootNode;
        private set => SetAndRaise(RootNodeProperty, ref _rootNode, value);
    }

    static ObjectViewer()
    {
        ValueProperty.Changed.AddClassHandler<ObjectViewer>((x, _) => x.OnValueChanged());
        MaxDepthProperty.Changed.AddClassHandler<ObjectViewer>((x, _) => x.OnValueChanged());
    }

    private void OnValueChanged()
    {
        RootNode = Value != null ? new ObjectNode(Value, null, MaxDepth) : null;
    }
}
