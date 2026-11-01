using System;
using System.Collections.Generic;
using EESimulator.Core.UI;
using Microsoft.Maui.Controls;

namespace EEE.Components.Common;

public partial class EETabView : ContentView
{
    private readonly List<string> _tabs = new();
    private int _selectedIndex = 0;

    public TabNode? Node { get; set; }

    public int SelectedIndex
    {
        get => _selectedIndex;
        set
        {
            if (_selectedIndex != value && value >= 0 && value < _tabs.Count)
            {
                _selectedIndex = value;
                RebuildTabs();
                if (Node != null) Node.ActiveTabIndex = value;
                TabChanged?.Invoke(this, _tabs[value]);
            }
        }
    }

    public string? SelectedTab => _selectedIndex >= 0 && _selectedIndex < _tabs.Count ? _tabs[_selectedIndex] : null;

    public event EventHandler<string>? TabChanged;

    public EETabView()
    {
        InitializeComponent();
        Node = new TabNode("Tabs");
    }

    public void AddTab(string title)
    {
        _tabs.Add(title);
        Node?.AddTab(title);
        RebuildTabs();
    }

    public void SetTabs(IEnumerable<string> tabs)
    {
        _tabs.Clear();
        _tabs.AddRange(tabs);
        if (_selectedIndex >= _tabs.Count) _selectedIndex = 0;
        RebuildTabs();
    }

    private void RebuildTabs()
    {
        TabButtonsHost.Children.Clear();
        for (int i = 0; i < _tabs.Count; i++)
        {
            int index = i;
            string title = _tabs[i];
            bool isSelected = index == _selectedIndex;

            var border = new Border
            {
                StrokeThickness = 1,
                Stroke = isSelected ? Color.FromArgb("#388BFD") : Colors.Transparent,
                BackgroundColor = isSelected ? Color.FromArgb("#1F6FEB") : Color.FromArgb("#21262D"),
                Padding = new Thickness(12, 6)
            };
            border.StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 6 };

            var label = new Label
            {
                Text = title,
                FontSize = 11,
                FontAttributes = isSelected ? FontAttributes.Bold : FontAttributes.None,
                TextColor = isSelected ? Colors.White : Color.FromArgb("#8B949E"),
                VerticalOptions = LayoutOptions.Center
            };
            border.Content = label;

            var tap = new TapGestureRecognizer();
            tap.Tapped += (s, e) => SelectedIndex = index;
            border.GestureRecognizers.Add(tap);

            TabButtonsHost.Children.Add(border);
        }
    }
}
