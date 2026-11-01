using System;
using EESimulator.Core.UI;
using Microsoft.Maui.Controls;

namespace EEE.Components.Common;

[ContentProperty(nameof(DialogBody))]
public partial class EEDialog : ContentView
{
    public static readonly BindableProperty TitleProperty =
        BindableProperty.Create(nameof(Title), typeof(string), typeof(EEDialog), "Dialog",
            propertyChanged: (b, o, n) => ((EEDialog)b).TitleLabel.Text = (string)n);

    public static readonly BindableProperty DialogBodyProperty =
        BindableProperty.Create(nameof(DialogBody), typeof(View), typeof(EEDialog), null,
            propertyChanged: (b, o, n) => ((EEDialog)b).DialogBodyHost.Content = (View)n);

    public static readonly BindableProperty IsOpenProperty =
        BindableProperty.Create(nameof(IsOpen), typeof(bool), typeof(EEDialog), false,
            BindingMode.TwoWay, propertyChanged: (b, o, n) => ((EEDialog)b).UpdateOpenState((bool)n));

    public DialogNode? Node { get; set; }

    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public View? DialogBody
    {
        get => (View?)GetValue(DialogBodyProperty);
        set => SetValue(DialogBodyProperty, value);
    }

    public bool IsOpen
    {
        get => (bool)GetValue(IsOpenProperty);
        set => SetValue(IsOpenProperty, value);
    }

    public event EventHandler? Confirmed;
    public event EventHandler? Cancelled;

    public EEDialog()
    {
        InitializeComponent();
        Node = new DialogNode("Dialog", Title);
    }

    private void UpdateOpenState(bool open)
    {
        IsVisible = open;
        if (Node != null) Node.IsOpen = open;
    }

    public void Open()
    {
        IsOpen = true;
        Node?.Open();
    }

    public void Close()
    {
        IsOpen = false;
        Node?.Close();
    }

    private void OnCloseTapped(object? sender, EventArgs e)
    {
        Close();
        Cancelled?.Invoke(this, EventArgs.Empty);
    }

    private void OnCancelClicked(object? sender, EventArgs e)
    {
        Close();
        Cancelled?.Invoke(this, EventArgs.Empty);
        Node?.Cancel();
    }

    private void OnConfirmClicked(object? sender, EventArgs e)
    {
        Close();
        Confirmed?.Invoke(this, EventArgs.Empty);
        Node?.Confirm();
    }
}
