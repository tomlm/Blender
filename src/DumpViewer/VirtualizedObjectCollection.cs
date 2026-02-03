using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;

namespace DumpViewer;

/// <summary>
/// A virtualized collection that lazily generates flattened nodes on-demand.
/// Only materializes nodes that are actually accessed (visible in the viewport).
/// Implements IList and INotifyCollectionChanged for Avalonia virtualization support.
/// </summary>
public class VirtualizedObjectCollection : IList<FlattenedNode>, IList, INotifyCollectionChanged, INotifyPropertyChanged
{
    private readonly List<ObjectNode> _rootNodes = new();
    private readonly Dictionary<int, FlattenedNode> _cache = new();
    private int _cachedCount = -1;
    private bool _countDirty = true;

    /// <summary>
    /// Gets the root nodes for direct tree traversal.
    /// </summary>
    public IReadOnlyList<ObjectNode> RootNodes => _rootNodes;

    /// <summary>
    /// Occurs when the collection changes.
    /// </summary>
    public event NotifyCollectionChangedEventHandler? CollectionChanged;

    /// <summary>
    /// Occurs when a property value changes.
    /// </summary>
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// Gets the number of visible (flattened) items.
    /// </summary>
    public int Count
    {
        get
        {
            if (_countDirty)
            {
                _cachedCount = CalculateVisibleCount();
                _countDirty = false;
            }
            return _cachedCount;
        }
    }

    public bool IsReadOnly => true;
    public bool IsFixedSize => false;
    public bool IsSynchronized => false;
    public object SyncRoot => this;

    /// <summary>
    /// Sets the root nodes and refreshes the collection.
    /// </summary>
    public void SetRootNodes(IEnumerable<ObjectNode> nodes)
    {
        _rootNodes.Clear();
        _rootNodes.AddRange(nodes);
        InvalidateAndNotify();
    }

    /// <summary>
    /// Clears all nodes.
    /// </summary>
    public void Clear()
    {
        _rootNodes.Clear();
        InvalidateAndNotify();
    }

    /// <summary>
    /// Adds a root node.
    /// </summary>
    public void AddRootNode(ObjectNode node)
    {
        _rootNodes.Add(node);
        InvalidateAndNotify();
    }

    /// <summary>
    /// Called when a node's IsExpanded state changes. Invalidates the cache and notifies listeners.
    /// </summary>
    public void OnNodeExpandedChanged(ObjectNode node)
    {
        InvalidateAndNotify();
    }

    /// <summary>
    /// Invalidates the cache and raises collection changed.
    /// </summary>
    public void InvalidateAndNotify()
    {
        _cache.Clear();
        _countDirty = true;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Count)));
        CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
    }

    /// <summary>
    /// Gets the flattened node at the specified index.
    /// </summary>
    public FlattenedNode this[int index]
    {
        get
        {
            if (index < 0 || index >= Count)
                throw new ArgumentOutOfRangeException(nameof(index));

            if (_cache.TryGetValue(index, out var cached))
                return cached;

            // Navigate to the item at the given index
            var result = GetNodeAtIndex(index);
            if (result != null)
            {
                _cache[index] = result;
            }
            return result ?? throw new InvalidOperationException($"Could not find node at index {index}");
        }
        set => throw new NotSupportedException("Collection is read-only");
    }

    object? IList.this[int index]
    {
        get => this[index];
        set => throw new NotSupportedException("Collection is read-only");
    }

    private int CalculateVisibleCount()
    {
        int count = 0;
        foreach (var root in _rootNodes)
        {
            count += CountVisibleNodes(root);
        }
        return count;
    }

    private static int CountVisibleNodes(ObjectNode node)
    {
        int count = 1; // Count this node

        if (node.IsExpanded && node.HasChildren)
        {
            // If children are already materialized, count them
            // Otherwise, use ChildCount to avoid creating all children just for counting
            if (node.ChildCount > 0)
            {
                foreach (var child in node.Children)
                {
                    count += CountVisibleNodes(child);
                }
            }
        }

        return count;
    }

    private FlattenedNode? GetNodeAtIndex(int targetIndex)
    {
        int currentIndex = 0;

        foreach (var root in _rootNodes)
        {
            var result = FindNodeAtIndex(root, 0, targetIndex, ref currentIndex);
            if (result != null)
                return result;
            
            // Early exit if we've passed the target
            if (currentIndex > targetIndex)
                break;
        }

        return null;
    }

    private static FlattenedNode? FindNodeAtIndex(ObjectNode node, int depth, int targetIndex, ref int currentIndex)
    {
        // Early exit - we've already passed the target index
        if (currentIndex > targetIndex)
            return null;

        if (currentIndex == targetIndex)
        {
            return new FlattenedNode(node, depth);
        }

        currentIndex++;

        if (node.IsExpanded && node.HasChildren)
        {
            foreach (var child in node.Children)
            {
                var result = FindNodeAtIndex(child, depth + 1, targetIndex, ref currentIndex);
                if (result != null)
                    return result;
                
                // Early exit if we've passed the target
                if (currentIndex > targetIndex)
                    return null;
            }
        }

        return null;
    }

    /// <summary>
    /// Finds the node with the largest StartLine that is less than or equal to the specified line.
    /// Searches the entire tree regardless of expansion state.
    /// </summary>
    public ObjectNode? FindNodeByLine(int line)
    {
        ObjectNode? bestMatch = null;
        int bestStartLine = int.MinValue;

        foreach (var root in _rootNodes)
        {
            FindNodeByLineRecursive(root, line, ref bestMatch, ref bestStartLine);
        }
        return bestMatch;
    }

    private static void FindNodeByLineRecursive(ObjectNode node, int targetLine, ref ObjectNode? bestMatch, ref int bestStartLine)
    {
        // Check if this node is a better match (larger StartLine that's still <= targetLine)
        if (node.HasSourceLocation && node.StartLine.HasValue)
        {
            int startLine = node.StartLine.Value;
            if (startLine <= targetLine && startLine > bestStartLine)
            {
                bestMatch = node;
                bestStartLine = startLine;
            }
        }

        // Always search children for potentially better matches
        if (node.HasChildren)
        {
            foreach (var child in node.Children)
            {
                FindNodeByLineRecursive(child, targetLine, ref bestMatch, ref bestStartLine);
            }
        }
    }

    /// <summary>
    /// Finds the index of a specific ObjectNode in the flattened view.
    /// </summary>
    public int IndexOfNode(ObjectNode node)
    {
        int currentIndex = 0;

        foreach (var root in _rootNodes)
        {
            var result = FindIndexOfNode(root, node, ref currentIndex);
            if (result >= 0)
                return result;
        }

        return -1;
    }

    private static int FindIndexOfNode(ObjectNode current, ObjectNode target, ref int currentIndex)
    {
        if (ReferenceEquals(current, target))
        {
            return currentIndex;
        }

        currentIndex++;

        if (current.IsExpanded && current.HasChildren)
        {
            foreach (var child in current.Children)
            {
                var result = FindIndexOfNode(child, target, ref currentIndex);
                if (result >= 0)
                    return result;
            }
        }

        return -1;
    }

    // IList<FlattenedNode> implementation
    public int IndexOf(FlattenedNode item) => IndexOfNode(item.Node);
    public bool Contains(FlattenedNode item) => IndexOf(item) >= 0;
    public void CopyTo(FlattenedNode[] array, int arrayIndex)
    {
        for (int i = 0; i < Count; i++)
        {
            array[arrayIndex + i] = this[i];
        }
    }

    public IEnumerator<FlattenedNode> GetEnumerator()
    {
        for (int i = 0; i < Count; i++)
        {
            yield return this[i];
        }
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    // Unsupported mutating operations
    public void Insert(int index, FlattenedNode item) => throw new NotSupportedException();
    public void RemoveAt(int index) => throw new NotSupportedException();
    public void Add(FlattenedNode item) => throw new NotSupportedException();
    public bool Remove(FlattenedNode item) => throw new NotSupportedException();
    void ICollection.CopyTo(Array array, int index) => CopyTo((FlattenedNode[])array, index);

    // IList implementation
    int IList.Add(object? value) => throw new NotSupportedException();
    bool IList.Contains(object? value) => value is FlattenedNode fn && Contains(fn);
    int IList.IndexOf(object? value) => value is FlattenedNode fn ? IndexOf(fn) : -1;
    void IList.Insert(int index, object? value) => throw new NotSupportedException();
    void IList.Remove(object? value) => throw new NotSupportedException();
    void IList.Clear() => Clear();
}
