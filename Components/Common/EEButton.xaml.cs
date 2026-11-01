using System;
using System.Windows.Input;
using EESimulator.Core.UI;
using Microsoft.Maui.Controls;

namespace EEE.Components.Common;

public partial class EEButton : ContentView
{
    public static readonly BindableProperty TextProperty =
        BindableProperty.Create(nameof(Text), typeof(string), typeof(EEButton), "Button",
            propertyChanged: (b, o, n) => ((EEButton)b).UpdateText((string)n));

    public static readonly BindableProperty IconProperty =
        BindableProperty.Create(nameof(Icon), typeof(string), typeof(EEButton), string.Empty,
            propertyChanged: (b, o, n) => ((EEButton)b).UpdateIcon((string)n));

    public static readonly BindableProperty VariantProperty =
        BindableProperty.Create(nameof(Variant), typeof(string), typeof(EEButton), "Secondary",
            propertyChanged: (b, o, n) => ((EEButton)b).UpdateVariant((string)n));

    public static readonly BindableProperty CommandProperty =
        BindableProperty.Create(nameof(Command), typeof(ICommand), typeof(EEButton), null);

    public static readonly BindableProperty CommandParameterProperty =
        BindableProperty.Create(nameof(CommandParameter), typeof(object), typeof(EEButton), null);

    public ButtonNode? Node { get; set; }

    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public string Icon
    {
        get => (string)GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    public string Variant
    {
        get => (string)GetValue(VariantProperty);
        set => SetValue(VariantProperty, value);
    }

    public ICommand? Command
    {
        get => (ICommand?)GetValue(CommandProperty);
        set => SetValue(CommandProperty, value);
    }

    public object? CommandParameter
    {
        get => GetValue(CommandParameterProperty);
        set => SetValue(CommandParameterProperty, value);
    }

    public event EventHandler? Clicked;

    public EEButton()
    {
        InitializeComponent();
        Node = new ButtonNode("Button", Text);
        UpdateVariant(Variant);
    }

    private void UpdateText(string text)
    {
        TextLabel.Text = text;
        if (Node != null) Node.Label = text;
    }

    private void UpdateIcon(string icon)
    {
        IconLabel.Text = icon;
        IconLabel.IsVisible = !string.IsNullOrEmpty(icon);
        if (Node != null) Node.Icon = icon;
    }

    private void UpdateVariant(string variant)
    {
        switch (variant?.ToLowerInvariant())
        {
            case "primary":
                ButtonBorder.BackgroundColor = Color.FromArgb("#238636");
                ButtonBorder.Stroke = Color.FromArgb("#2EA043");
                TextLabel.TextColor = Colors.White;
                break;
            case "accent":
                ButtonBorder.BackgroundColor = Color.FromArgb("#1F6FEB");
                ButtonBorder.Stroke = Color.FromArgb("#388BFD");
                TextLabel.TextColor = Colors.White;
                break;
            case "danger":
                ButtonBorder.BackgroundColor = Color.FromArgb("#DA3633");
                ButtonBorder.Stroke = Color.FromArgb("#F85149");
                TextLabel.TextColor = Colors.White;
                break;
            default: // Secondary
                ButtonBorder.BackgroundColor = Color.FromArgb("#21262D");
                ButtonBorder.Stroke = Color.FromArgb("#30363D");
                TextLabel.TextColor = Color.FromArgb("#E6EDF3");
                break;
        }
    }

    private void OnTapped(object? sender, EventArgs e)
    {
        Clicked?.Invoke(this, EventArgs.Empty);
        Node?.Click();
        if (Command?.CanExecute(CommandParameter) == true)
        {
            Command.Execute(CommandParameter);
        }
    }
}
