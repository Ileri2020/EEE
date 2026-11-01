using System;
using System.Collections;
using System.Collections.Generic;
using EESimulator.Core.UI;
using Microsoft.Maui.Controls;

namespace EEE.Components.Common;

public partial class EEDropdown : ContentView
{
    public static readonly BindableProperty ItemsSourceProperty =
        BindableProperty.Create(nameof(ItemsSource), typeof(IList), typeof(EEDropdown), null,
            propertyChanged: (b, o, n) => ((EEDropdown)b).OnItemsSourceChanged((IList)n));

    public static readonly BindableProperty SelectedItemProperty =
        BindableProperty.Create(nameof(SelectedItem), typeof(object), typeof(EEDropdown), null,
            BindingMode.TwoWay, propertyChanged: (b, o, n) => ((EEDropdown)b).OnSelectedItemChanged(n));

    public static readonly BindableProperty TitleProperty =
        BindableProperty.Create(nameof(Title), typeof(string), typeof(EEDropdown), "Select...",
            propertyChanged: (b, o, n) => ((EEDropdown)b).InnerPicker.Title = (string)n);

    public static readonly BindableProperty LabelProperty =
        BindableProperty.Create(nameof(Label), typeof(string), typeof(EEDropdown), string.Empty,
            propertyChanged: (b, o, n) => ((EEDropdown)b).OnLabelChanged((string)n));

    public DropdownNode? Node { get; set; }

    public IList? ItemsSource
    {
        get => (IList?)GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }

    public object? SelectedItem
    {
        get => GetValue(SelectedItemProperty);
        set => SetValue(SelectedItemProperty, value);
    }

    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public string Label
    {
        get => (string)GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    public event EventHandler<object?>? SelectionChanged;

    public EEDropdown()
    {
        InitializeComponent();
        Node = new DropdownNode("Dropdown");
    }

    private void OnItemsSourceChanged(IList list)
    {
        InnerPicker.ItemsSource = list;
        if (Node != null && list != null)
        {
            Node.Options.Clear();
            foreach (var item in list)
            {
                if (item != null) Node.Options.Add(item.ToString()!);
            }
        }
    }

    private void OnSelectedItemChanged(object? item)
    {
        if (InnerPicker.SelectedItem != item)
        {
            InnerPicker.SelectedItem = item;
        }
        if (Node != null && item != null)
        {
            Node.SelectedItem = item.ToString();
        }
    }

    private void OnLabelChanged(string label)
    {
        FieldLabel.Text = label;
        FieldLabel.IsVisible = !string.IsNullOrEmpty(label);
    }

    private void OnSelectedIndexChanged(object? sender, EventArgs e)
    {
        SelectedItem = InnerPicker.SelectedItem;
        if (Node != null && InnerPicker.SelectedItem != null)
        {
            Node.SelectedItem = InnerPicker.SelectedItem.ToString();
        }
        SelectionChanged?.Invoke(this, InnerPicker.SelectedItem);
    }
}
