using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using AvaloniaEdit;
using AvaloniaEdit.Document;
using AvaloniaEdit.Highlighting;
using System;
using System.Collections.Generic;
using System.Linq;

namespace DumpViewer;


/// <summary>
/// A control that visualizes any object in a tree structure similar to LinqPad's .Dump().
/// Uses a flat virtualized list for efficient handling of large datasets (400k+ items).
/// </summary>
public class ObjectViewer : TemplatedControl
{
    private ListBox? _listBox;
    private TextEditor? _textEditor;
    private bool _isSyncingFromTree;
    private bool _isSyncingFromEditor;

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
    /// Defines the <see cref="SourceText"/> property.
    /// </summary>
    public static readonly StyledProperty<string?> SourceTextProperty =
        AvaloniaProperty.Register<ObjectViewer, string?>(nameof(SourceText));

    /// <summary>
    /// Defines the <see cref="SyntaxHighlighting"/> property (json, yaml, xml, csv).
    /// </summary>
    public static readonly StyledProperty<string?> SyntaxHighlightingProperty =
        AvaloniaProperty.Register<ObjectViewer, string?>(nameof(SyntaxHighlighting));

    /// <summary>
    /// Defines the <see cref="SelectedNode"/> property.
    /// </summary>
    public static readonly StyledProperty<ObjectNode?> SelectedNodeProperty =
        AvaloniaProperty.Register<ObjectViewer, ObjectNode?>(nameof(SelectedNode));

    /// <summary>
    /// Defines the <see cref="FlattenedItems"/> property for the virtualized collection.
    /// </summary>
    public static readonly DirectProperty<ObjectViewer, VirtualizedObjectCollection> FlattenedItemsProperty =
        AvaloniaProperty.RegisterDirect<ObjectViewer, VirtualizedObjectCollection>(
            nameof(FlattenedItems),
            o => o.FlattenedItems);

    /// <summary>
    /// Defines the <see cref="SelectedSourceRange"/> property.
    /// </summary>
    public static readonly DirectProperty<ObjectViewer, SourceRange?> SelectedSourceRangeProperty =
        AvaloniaProperty.RegisterDirect<ObjectViewer, SourceRange?>(
            nameof(SelectedSourceRange),
            o => o.SelectedSourceRange);

    private readonly VirtualizedObjectCollection _flattenedItems = new();
    private SourceRange? _selectedSourceRange;

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
    /// Gets or sets the raw source text for highlighting.
    /// </summary>
    public string? SourceText
    {
        get => GetValue(SourceTextProperty);
        set => SetValue(SourceTextProperty, value);
    }

    /// <summary>
    /// Gets or sets the syntax highlighting format (json, yaml, xml, csv).
    /// </summary>
    public string? SyntaxHighlighting
    {
        get => GetValue(SyntaxHighlightingProperty);
        set => SetValue(SyntaxHighlightingProperty, value);
    }

    /// <summary>
    /// Gets or sets the currently selected node.
    /// </summary>
    public ObjectNode? SelectedNode
    {
        get => GetValue(SelectedNodeProperty);
        set => SetValue(SelectedNodeProperty, value);
    }

    /// <summary>
    /// Gets the source range for the currently selected node.
    /// </summary>
    public SourceRange? SelectedSourceRange
    {
        get => _selectedSourceRange;
        private set => SetAndRaise(SelectedSourceRangeProperty, ref _selectedSourceRange, value);
    }

    /// <summary>
    /// Gets the virtualized flattened collection of nodes.
    /// </summary>
    public VirtualizedObjectCollection FlattenedItems => _flattenedItems;


    static ObjectViewer()
    {
        ValueProperty.Changed.AddClassHandler<ObjectViewer>((x, _) => x.OnValueChanged());
        MaxDepthProperty.Changed.AddClassHandler<ObjectViewer>((x, _) => x.OnValueChanged());
        SourceTextProperty.Changed.AddClassHandler<ObjectViewer>((x, _) => x.OnSourceTextChanged());
        SyntaxHighlightingProperty.Changed.AddClassHandler<ObjectViewer>((x, _) => x.OnSyntaxHighlightingChanged());
        SelectedNodeProperty.Changed.AddClassHandler<ObjectViewer>((x, _) => x.OnSelectedNodeChanged());
        FocusableProperty.OverrideDefaultValue<ObjectViewer>(true);
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);

        // Unsubscribe from old controls
        if (_listBox != null)
        {
            _listBox.SelectionChanged -= OnListBoxSelectionChanged;
            _listBox.DoubleTapped -= OnListBoxDoubleTapped;
            _listBox.RemoveHandler(KeyDownEvent, OnListBoxKeyDown);
        }
        if (_textEditor != null)
        {
            _textEditor.TextArea.Caret.PositionChanged -= OnEditorCaretPositionChanged;
        }

        // Get new controls
        _listBox = e.NameScope.Find<ListBox>("PART_ListBox");
        _textEditor = e.NameScope.Find<TextEditor>("PART_TextEditor");

        // Subscribe to events
        if (_listBox != null)
        {
            _listBox.SelectionChanged += OnListBoxSelectionChanged;
            _listBox.DoubleTapped += OnListBoxDoubleTapped;
            _listBox.AddHandler(KeyDownEvent, OnListBoxKeyDown, RoutingStrategies.Tunnel);
        }
        if (_textEditor != null)
        {
            _textEditor.TextArea.Caret.PositionChanged += OnEditorCaretPositionChanged;
            ConfigureTextEditor();
            UpdateTextEditorContent();
        }
    }

    private void OnListBoxDoubleTapped(object? sender, TappedEventArgs e)
    {
        // Toggle expand/collapse on double-click
        if (_listBox?.SelectedItem is FlattenedNode flatNode && flatNode.CanExpand)
        {
            var nodeToRestore = flatNode.Node;
            flatNode.Node.IsExpanded = !flatNode.Node.IsExpanded;
            _flattenedItems.OnNodeExpandedChanged(flatNode.Node);
            RestoreSelectionAndFocus(nodeToRestore);
        }
    }

    private void OnListBoxKeyDown(object? sender, KeyEventArgs e)
    {
        if (_listBox?.SelectedItem is not FlattenedNode flatNode)
            return;

        bool handled = false;
        var nodeToSelect = flatNode.Node;

        if (e.Key == Key.Right && flatNode.CanExpand && !flatNode.IsExpanded)
        {
            // Right arrow expands a collapsed node
            flatNode.Node.IsExpanded = true;
            _flattenedItems.OnNodeExpandedChanged(flatNode.Node);
            handled = true;
        }
        else if (e.Key == Key.Left && flatNode.CanExpand && flatNode.IsExpanded)
        {
            // Left arrow collapses an expanded node
            flatNode.Node.IsExpanded = false;
            _flattenedItems.OnNodeExpandedChanged(flatNode.Node);
            handled = true;
        }
        else if ((e.Key == Key.Enter || e.Key == Key.Space) && flatNode.CanExpand)
        {
            // Enter or Space toggles expand/collapse
            flatNode.Node.IsExpanded = !flatNode.Node.IsExpanded;
            _flattenedItems.OnNodeExpandedChanged(flatNode.Node);
            handled = true;
        }

        if (handled)
        {
            e.Handled = true;
            RestoreSelectionAndFocus(nodeToSelect);
        }
    }

    /// <summary>
    /// Restores selection and focus to a specific node after the collection has been reset.
    /// </summary>
    private void RestoreSelectionAndFocus(ObjectNode node)
    {
        // Schedule restoration after the layout pass completes
        Dispatcher.UIThread.Post(() =>
        {
            if (_listBox == null) return;

            // Find the node's new index in the flattened collection
            var index = _flattenedItems.IndexOfNode(node);
            if (index >= 0)
            {
                _listBox.SelectedIndex = index;
                _listBox.ScrollIntoView(index);

                // Try to focus the actual ListBoxItem container for more reliable focus
                Dispatcher.UIThread.Post(() =>
                {
                    if (_listBox.ContainerFromIndex(index) is ListBoxItem item)
                    {
                        item.Focus();
                    }
                    else
                    {
                        _listBox.Focus();
                    }
                }, DispatcherPriority.Input);
            }
        }, DispatcherPriority.Loaded);
    }

    private void OnListBoxSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_isSyncingFromEditor) return;

        if (e.AddedItems.Count > 0 && e.AddedItems[0] is FlattenedNode flatNode)
        {
            SelectedNode = flatNode.Node;
        }
    }

    /// <summary>
    /// Toggles the expanded state of a node.
    /// </summary>
    public void ToggleNode(ObjectNode node)
    {
        node.IsExpanded = !node.IsExpanded;
        _flattenedItems.OnNodeExpandedChanged(node);
    }

    private void ConfigureTextEditor()
    {
        if (_textEditor == null) return;

        _textEditor.IsReadOnly = true;
        _textEditor.ShowLineNumbers = true;
        _textEditor.FontFamily = new FontFamily("Consolas, Monaco, 'Courier New', monospace");
        _textEditor.FontSize = 13;
        _textEditor.Background = Brushes.Transparent;
        _textEditor.Foreground = new SolidColorBrush(Color.Parse("#D4D4D4"));
    }






    private void UpdateTextEditorContent()
    {
        if (_textEditor == null) return;

        // Suppress caret position changed events while updating document
        // to avoid expensive FindNodeAtLine calls during document swap
        _isSyncingFromTree = true;
        try
        {
            // Set text content using Document for proper rendering
            var text = SourceText ?? string.Empty;
            
            // Check for very long lines that would cause rendering issues
            // AvaloniaEdit struggles with lines that are millions of characters
            text = EnsureReasonableLineLength(text);
            
            System.Diagnostics.Debug.WriteLine($"[ObjectViewer] Setting document text: {text.Length} chars, {text.Split('\n').Length} lines");
            
            // Always create a fresh document to avoid rendering issues
            _textEditor.Document = new TextDocument(text);
            
            System.Diagnostics.Debug.WriteLine($"[ObjectViewer] Document created: LineCount={_textEditor.Document.LineCount}");

            // Apply syntax highlighting only for reasonably sized files
            // Large files cause regex-based highlighting to hang
            const int MaxHighlightingLength = 500_000; // 500KB max for syntax highlighting
            IHighlightingDefinition? highlighting = null;
            
            if (text.Length <= MaxHighlightingLength)
            {
                highlighting = SyntaxHighlightingManager.GetHighlightingForFormat(SyntaxHighlighting);
            }
            
            if (_textEditor.SyntaxHighlighting != highlighting)
            {
                _textEditor.SyntaxHighlighting = highlighting;
            }
        }
        finally
        {
            _isSyncingFromTree = false;
        }
    }

    /// <summary>
    /// Ensures no single line exceeds a reasonable length for rendering.
    /// For XML/JSON, attempts to format. For other content, truncates long lines.
    /// </summary>
    private string EnsureReasonableLineLength(string text)
    {
        const int MaxLineLength = 10_000; // 10K chars max per line
        const int MaxTotalLength = 10_000_000; // 10MB max total
        
        // Quick check - if text is small, no need to process
        if (text.Length <= MaxLineLength)
            return text;
        
        // Check if any line exceeds the max
        var lines = text.Split('\n');
        bool hasLongLine = false;
        foreach (var line in lines)
        {
            if (line.Length > MaxLineLength)
            {
                hasLongLine = true;
                break;
            }
        }
        
        if (!hasLongLine)
            return text;
        
        // Try to format XML if it looks like XML
        if (SyntaxHighlighting == "xml" && text.TrimStart().StartsWith('<'))
        {
            try
            {
                var doc = new System.Xml.XmlDocument();
                doc.LoadXml(text);
                
                using var sw = new System.IO.StringWriter();
                using var xw = new System.Xml.XmlTextWriter(sw);
                xw.Formatting = System.Xml.Formatting.Indented;
                xw.Indentation = 2;
                doc.WriteTo(xw);
                
                var formatted = sw.ToString();
                
                // Only use formatted if it's not too large
                if (formatted.Length <= MaxTotalLength)
                {
                    System.Diagnostics.Debug.WriteLine($"[ObjectViewer] Formatted XML: {text.Length} -> {formatted.Length} chars");
                    return formatted;
                }
            }
            catch
            {
                // If formatting fails, fall through to truncation
            }
        }
        
        // Try to format JSON if it looks like JSON
        if (SyntaxHighlighting == "json" && (text.TrimStart().StartsWith('{') || text.TrimStart().StartsWith('[')))
        {
            try
            {
                var obj = Newtonsoft.Json.Linq.JToken.Parse(text);
                var formatted = obj.ToString(Newtonsoft.Json.Formatting.Indented);
                
                if (formatted.Length <= MaxTotalLength)
                {
                    System.Diagnostics.Debug.WriteLine($"[ObjectViewer] Formatted JSON: {text.Length} -> {formatted.Length} chars");
                    return formatted;
                }
            }
            catch
            {
                // If formatting fails, fall through to truncation
            }
        }
        
        // If formatting didn't work, truncate long lines
        var result = new System.Text.StringBuilder();
        foreach (var line in lines)
        {
            if (line.Length > MaxLineLength)
            {
                result.AppendLine(line.Substring(0, MaxLineLength) + "... (line truncated)");
            }
            else
            {
                result.AppendLine(line);
            }
            
            // Stop if result is too large
            if (result.Length > MaxTotalLength)
            {
                result.AppendLine("\n... (content truncated for display)");
                break;
            }
        }
        
        return result.ToString();
    }

    private void OnSourceTextChanged()
    {
        UpdateTextEditorContent();
    }


    private void OnSyntaxHighlightingChanged()
    {
        UpdateTextEditorContent();
    }

    private void OnEditorCaretPositionChanged(object? sender, EventArgs e)
    {
        if (_isSyncingFromTree || _textEditor == null) return;

        _isSyncingFromEditor = true;
        try
        {
            var line = _textEditor.TextArea.Caret.Line;
            var node = FindNodeAtLine(line);
            if (node != null && node != SelectedNode)
            {
                SelectedNode = node;
                ExpandToNode(node);
                SelectNodeInListBox(node);
            }
        }
        finally
        {
            _isSyncingFromEditor = false;
        }
    }

    private ObjectNode? FindNodeAtLine(int line)
    {
        // Search through the root nodes recursively (not through the virtualized collection)
        // This avoids O(n²) behavior when iterating through a large virtualized list
        return _flattenedItems.FindNodeByLine(line);
    }

    private void ExpandToNode(ObjectNode targetNode)
    {
        // Expand all ancestors by searching from the root nodes directly
        // (not through the flattened collection, which only contains visible nodes)
        bool changed = false;
        foreach (var rootNode in _flattenedItems.RootNodes)
        {
            if (ExpandAncestorsRecursive(rootNode, targetNode, ref changed))
                break;
        }
        if (changed)
        {
            _flattenedItems.InvalidateAndNotify();
        }
    }

    private bool ExpandAncestorsRecursive(ObjectNode current, ObjectNode target, ref bool changed)
    {
        if (current == target)
            return true;

        if (current.HasChildren)
        {
            foreach (var child in current.Children)
            {
                if (child == target || ExpandAncestorsRecursive(child, target, ref changed))
                {
                    if (!current.IsExpanded)
                    {
                        current.IsExpanded = true;
                        changed = true;
                    }
                    return true;
                }
            }
        }
        return false;
    }

    private void SelectNodeInListBox(ObjectNode node)
    {
        if (_listBox == null) return;
        
        var index = _flattenedItems.IndexOfNode(node);
        if (index >= 0 && index < _flattenedItems.Count)
        {
            _listBox.SelectedIndex = index;
            _listBox.ScrollIntoView(index);
        }
    }

    private void OnSelectedNodeChanged()
    {
        if (_isSyncingFromEditor) return;
        UpdateSelectedSourceRange();
    }




    private void UpdateSelectedSourceRange()
    {
        if (SelectedNode == null || string.IsNullOrEmpty(SourceText) || !SelectedNode.HasSourceLocation)
        {
            SelectedSourceRange = null;
            return;
        }

        var startLine = SelectedNode.StartLine!.Value;
        var startCol = SelectedNode.StartColumn ?? 1;
        var endLine = SelectedNode.EndLine ?? startLine;
        var endCol = SelectedNode.EndColumn ?? 1;

        // Convert line/column to character offsets
        var (startOffset, endOffset) = GetCharacterOffsets(startLine, startCol, endLine, endCol);

        SelectedSourceRange = new SourceRange(startLine, startCol, endLine, endCol, startOffset, endOffset);

        // Update the text editor selection and scroll into view
        if (_textEditor != null && startOffset >= 0)
        {
            _isSyncingFromTree = true;
            try
            {

//                _textEditor.TextArea.Selection = Selection.Create(_textEditor.TextArea, startOffset, endOffset);
                _textEditor.TextArea.Caret.Offset = startOffset;

                // Scroll to make the line visible
                _textEditor.ScrollToLine(startLine);
            }
            finally
            {
                _isSyncingFromTree = false;
            }
        }
    }

    private (int startOffset, int endOffset) GetCharacterOffsets(int startLine, int startCol, int endLine, int endCol)
    {
        return (_textEditor.Document.GetOffset(startLine, startCol), _textEditor.Document.GetOffset(endLine, endCol));
    }

    private void OnValueChanged()
    {
        _flattenedItems.Clear();
        if (Value != null)
        {
            var rootNode = new ObjectNode(Value, null, MaxDepth);
            _flattenedItems.AddRootNode(rootNode);
        }
    }
}
