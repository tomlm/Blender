using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace DumpViewer;

/// <summary>
/// A control that visualizes any object in a tree structure similar to LinqPad's .Dump().
/// </summary>
public class ObjectViewer : TemplatedControl
{
    private TextBox? _searchTextBox;
    private Button? _searchButton;
    
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
    /// Defines the <see cref="Items"/> property.
    /// </summary>
    public static readonly DirectProperty<ObjectViewer, ObservableCollection<ObjectNode>> ItemsProperty =
        AvaloniaProperty.RegisterDirect<ObjectViewer, ObservableCollection<ObjectNode>>(
            nameof(Items),
            o => o.Items);

    private ObservableCollection<ObjectNode> _items = [];

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
    /// Gets the collection of root nodes for the TreeView.
    /// </summary>
    public ObservableCollection<ObjectNode> Items
    {
        get => _items;
        private set => SetAndRaise(ItemsProperty, ref _items, value);
    }

    static ObjectViewer()
    {
        ValueProperty.Changed.AddClassHandler<ObjectViewer>((x, _) => x.OnValueChanged());
        MaxDepthProperty.Changed.AddClassHandler<ObjectViewer>((x, _) => x.OnValueChanged());
        FocusableProperty.OverrideDefaultValue<ObjectViewer>(true);
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        
        // Unsubscribe from old controls
        if (_searchTextBox != null)
        {
            _searchTextBox.KeyDown -= OnSearchTextBoxKeyDown;
        }
        if (_searchButton != null)
        {
            _searchButton.Click -= OnSearchButtonClick;
        }
        
        // Get new controls
        _searchTextBox = e.NameScope.Find<TextBox>("PART_SearchTextBox");
        _searchButton = e.NameScope.Find<Button>("PART_SearchButton");
        
        // Subscribe to events
        if (_searchTextBox != null)
        {
            _searchTextBox.KeyDown += OnSearchTextBoxKeyDown;
        }
        if (_searchButton != null)
        {
            _searchButton.Click += OnSearchButtonClick;
        }
    }

    private void OnSearchTextBoxKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            PerformSearch();
            e.Handled = true;
        }
    }

    private void OnSearchButtonClick(object? sender, RoutedEventArgs e)
    {
        PerformSearch();
    }

    private void PerformSearch()
    {
        var searchText = _searchTextBox?.Text;
        // TODO: Implement search functionality
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        
        if (e.Key == Key.F && e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            _searchTextBox?.Focus();
            e.Handled = true;
        }
    }

    private void OnValueChanged()
    {
        _items.Clear();
        if (Value != null)
        {
            _items.Add(new ObjectNode(Value, null, MaxDepth));
        }
    }
}
