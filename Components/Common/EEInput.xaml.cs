using System;
using EESimulator.Core.UI;
using Microsoft.Maui.Controls;

namespace EEE.Components.Common;

public partial class EEInput : ContentView
{
    public static readonly BindableProperty TextProperty =
        BindableProperty.Create(nameof(Text), typeof(string), typeof(EEInput), string.Empty,
            BindingMode.TwoWay, propertyChanged: (b, o, n) => ((EEInput)b).OnTextPropertyChanged((string)n));

    public static readonly BindableProperty PlaceholderProperty =
        BindableProperty.Create(nameof(Placeholder), typeof(string), typeof(EEInput), "Enter text...",
            propertyChanged: (b, o, n) => ((EEInput)b).InnerEntry.Placeholder = (string)n);

    public static readonly BindableProperty LabelProperty =
        BindableProperty.Create(nameof(Label), typeof(string), typeof(EEInput), string.Empty,
            propertyChanged: (b, o, n) => ((EEInput)b).OnLabelPropertyChanged((string)n));

    public InputNode? Node { get; set; }

    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public string Placeholder
    {
        get => (string)GetValue(PlaceholderProperty);
        set => SetValue(PlaceholderProperty, value);
    }

    public string Label
    {
        get => (string)GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    public event EventHandler<string>? TextSubmitted;

    public EEInput()
    {
        InitializeComponent();
        Node = new InputNode("Input", Placeholder);
    }

    private void OnTextPropertyChanged(string text)
    {
        if (InnerEntry.Text != text)
        {
            InnerEntry.Text = text;
        }
        ClearButton.IsVisible = !string.IsNullOrEmpty(text);
        if (Node != null) Node.Text = text;
    }

    private void OnLabelPropertyChanged(string label)
    {
        FieldLabel.Text = label;
        FieldLabel.IsVisible = !string.IsNullOrEmpty(label);
    }

    private void OnTextChanged(object? sender, TextChangedEventArgs e)
    {
        Text = e.NewTextValue;
        ClearButton.IsVisible = !string.IsNullOrEmpty(e.NewTextValue);
        Node?.SetText(e.NewTextValue);
        TextSubmitted?.Invoke(this, e.NewTextValue);
    }

    private void OnClearTapped(object? sender, EventArgs e)
    {
        Text = string.Empty;
        InnerEntry.Text = string.Empty;
        Node?.Clear();
    }
}
