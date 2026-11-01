using System;
using System.Collections;
using System.Collections.Generic;
using EESimulator.Core.UI;
using Microsoft.Maui.Controls;

namespace EEE.Components.Common;

public partial class EESelect : ContentView
{
    private readonly List<string> _items = new();
    private readonly HashSet<string> _selected = new();

    public static readonly BindableProperty MultiSelectProperty =
        BindableProperty.Create(nameof(MultiSelect), typeof(bool), typeof(EESelect), false);

    public SelectNode? Node { get; set; }

    public bool MultiSelect
    {
        get => (bool)GetValue(MultiSelectProperty);
        set => SetValue(MultiSelectProperty, value);
    }

    public string? SelectedItem => _selected.Count > 0 ? _items.Find(i => _selected.Contains(i)) : null;

    public event EventHandler<string>? SelectionChanged;

    public EESelect()
    {
        InitializeComponent();
        Node = new SelectNode("Select", multiSelect: MultiSelect);
    }

    public void SetItems(IEnumerable<string> items, string? initialSelected = null)
    {
        _items.Clear();
        _selected.Clear();
        _items.AddRange(items);
        if (!string.IsNullOrEmpty(initialSelected) && _items.Contains(initialSelected))
        {
            _selected.Add(initialSelected);
        }
        else if (_items.Count > 0 && !MultiSelect)
        {
            _selected.Add(_items[0]);
        }
        RebuildChips();
    }

    private void RebuildChips()
    {
        ChipsHost.Children.Clear();
        foreach (var item in _items)
        {
            string itemName = item;
            bool isSelected = _selected.Contains(itemName);

            var border = new Border
            {
                StrokeThickness = 1,
                Stroke = isSelected ? Color.FromArgb("#58A6FF") : Color.FromArgb("#30363D"),
                BackgroundColor = isSelected ? Color.FromArgb("#1F6FEB") : Color.FromArgb("#21262D"),
                Padding = new Thickness(10, 5),
                Margin = new Thickness(3)
            };
            border.StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 14 };

            var label = new Label
            {
                Text = itemName,
                FontSize = 11,
                TextColor = isSelected ? Colors.White : Color.FromArgb("#C9D1D9"),
                VerticalOptions = LayoutOptions.Center
            };
            border.Content = label;

            var tap = new TapGestureRecognizer();
            tap.Tapped += (s, e) =>
            {
                if (!MultiSelect)
                {
                    _selected.Clear();
                    _selected.Add(itemName);
                }
                else
                {
                    if (!_selected.Add(itemName))
                    {
                        _selected.Remove(itemName);
                    }
                }
                RebuildChips();
                Node?.Select(itemName);
                SelectionChanged?.Invoke(this, itemName);
            };
            border.GestureRecognizers.Add(tap);

            ChipsHost.Children.Add(border);
        }
    }
}
