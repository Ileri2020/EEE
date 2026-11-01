using System;
using EESimulator.Core.UI;
using Microsoft.Maui.Controls;

namespace EEE.Components.Common;

[ContentProperty(nameof(SidebarContent))]
public partial class EESidebar : ContentView
{
    public static readonly BindableProperty TitleProperty =
        BindableProperty.Create(nameof(Title), typeof(string), typeof(EESidebar), "Sidebar",
            propertyChanged: (b, o, n) => ((EESidebar)b).TitleLabel.Text = (string)n);

    public static readonly BindableProperty IsCollapsedProperty =
        BindableProperty.Create(nameof(IsCollapsed), typeof(bool), typeof(EESidebar), false,
            propertyChanged: (b, o, n) => ((EESidebar)b).UpdateCollapsedState((bool)n));

    public static readonly BindableProperty SidebarContentProperty =
        BindableProperty.Create(nameof(SidebarContent), typeof(View), typeof(EESidebar), null,
            propertyChanged: (b, o, n) => ((EESidebar)b).ContentHost.Content = (View)n);

    public SidebarNode? Node { get; set; }

    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public bool IsCollapsed
    {
        get => (bool)GetValue(IsCollapsedProperty);
        set => SetValue(IsCollapsedProperty, value);
    }

    public View? SidebarContent
    {
        get => (View?)GetValue(SidebarContentProperty);
        set => SetValue(SidebarContentProperty, value);
    }

    public event EventHandler<bool>? CollapsedChanged;

    public EESidebar()
    {
        InitializeComponent();
        Node = new SidebarNode("Sidebar");
    }

    private void UpdateCollapsedState(bool collapsed)
    {
        SidebarBorder.WidthRequest = collapsed ? 44 : 260;
        ToggleIcon.Text = collapsed ? "▶" : "◀";
        ContentHost.IsVisible = !collapsed;
        TitleLabel.IsVisible = !collapsed;
        if (Node != null) Node.IsCollapsed = collapsed;
        CollapsedChanged?.Invoke(this, collapsed);
    }

    private void OnToggleTapped(object? sender, EventArgs e)
    {
        IsCollapsed = !IsCollapsed;
    }
}
