using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.VisualTree;

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

    static NodeView()
    {
        FocusableProperty.OverrideDefaultValue<NodeView>(true);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        if (Node == null)
            return;

        switch (e.Key)
        {
            case Key.Enter:
            case Key.Space:
                // Toggle expand/collapse
                if (Node.IsExpandable)
                {
                    Node.IsExpanded = !Node.IsExpanded;
                    e.Handled = true;
                }
                break;

            case Key.Right:
                // Expand or move to first child
                if (Node.IsExpandable && !Node.IsExpanded)
                {
                    Node.IsExpanded = true;
                    e.Handled = true;
                }
                else if (Node.IsExpanded && Node.Children.Count > 0)
                {
                    // Move focus to first child
                    var firstChild = FindChildNodeView(0);
                    firstChild?.Focus();
                    e.Handled = true;
                }
                break;

            case Key.Left:
                // Collapse or move to parent
                if (Node.IsExpanded)
                {
                    Node.IsExpanded = false;
                    e.Handled = true;
                }
                else
                {
                    // Move focus to parent
                    var parent = FindParentNodeView();
                    parent?.Focus();
                    e.Handled = true;
                }
                break;

            case Key.Down:
                // Move to next visible node
                MoveToNextNode();
                e.Handled = true;
                break;

            case Key.Up:
                // Move to previous visible node
                MoveToPreviousNode();
                e.Handled = true;
                break;

            case Key.Home:
                // Move to root
                var root = FindRootNodeView();
                root?.Focus();
                e.Handled = true;
                break;
        }
    }

    private NodeView? FindChildNodeView(int index)
    {
        // Find the ItemsControl that contains our children
        var itemsControl = this.FindDescendantOfType<ItemsControl>();
        if (itemsControl == null || index >= Node?.Children.Count)
            return null;

        var container = itemsControl.ContainerFromIndex(index);
        return container?.FindDescendantOfType<NodeView>();
    }

    private NodeView? FindParentNodeView()
    {
        // Walk up the visual tree to find the parent NodeView
        var current = this.GetVisualParent();
        while (current != null)
        {
            if (current is NodeView parentNode && parentNode != this)
                return parentNode;
            current = current.GetVisualParent();
        }
        return null;
    }

    private NodeView? FindRootNodeView()
    {
        NodeView? root = this;
        var parent = FindParentNodeView();
        while (parent != null)
        {
            root = parent;
            parent = parent.FindParentNodeView();
        }
        return root;
    }

    private void MoveToNextNode()
    {
        // If expanded and has children, go to first child
        if (Node?.IsExpanded == true && Node.Children.Count > 0)
        {
            var firstChild = FindChildNodeView(0);
            firstChild?.Focus();
            return;
        }

        // Otherwise, find next sibling or parent's next sibling
        var current = this;
        while (current != null)
        {
            var nextSibling = current.FindNextSibling();
            if (nextSibling != null)
            {
                nextSibling.Focus();
                return;
            }
            current = current.FindParentNodeView();
        }
    }

    private void MoveToPreviousNode()
    {
        // Find previous sibling
        var prevSibling = FindPreviousSibling();
        if (prevSibling != null)
        {
            // Go to the last visible descendant of the previous sibling
            var lastVisible = prevSibling.FindLastVisibleDescendant();
            lastVisible.Focus();
            return;
        }

        // No previous sibling, go to parent
        var parent = FindParentNodeView();
        parent?.Focus();
    }

    private NodeView? FindNextSibling()
    {
        var parent = FindParentNodeView();
        if (parent == null)
            return null;

        var siblings = parent.Node?.Children;
        if (siblings == null)
            return null;

        // Find our index among siblings
        var myIndex = -1;
        for (int i = 0; i < siblings.Count; i++)
        {
            if (siblings[i] == Node)
            {
                myIndex = i;
                break;
            }
        }

        if (myIndex >= 0 && myIndex < siblings.Count - 1)
        {
            return parent.FindChildNodeView(myIndex + 1);
        }

        return null;
    }

    private NodeView? FindPreviousSibling()
    {
        var parent = FindParentNodeView();
        if (parent == null)
            return null;

        var siblings = parent.Node?.Children;
        if (siblings == null)
            return null;

        // Find our index among siblings
        var myIndex = -1;
        for (int i = 0; i < siblings.Count; i++)
        {
            if (siblings[i] == Node)
            {
                myIndex = i;
                break;
            }
        }

        if (myIndex > 0)
        {
            return parent.FindChildNodeView(myIndex - 1);
        }

        return null;
    }

    private NodeView FindLastVisibleDescendant()
    {
        if (Node?.IsExpanded != true || Node.Children.Count == 0)
            return this;

        var lastChild = FindChildNodeView(Node.Children.Count - 1);
        return lastChild?.FindLastVisibleDescendant() ?? this;
    }
}
