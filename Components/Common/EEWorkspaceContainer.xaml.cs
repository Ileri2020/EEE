using System;
using EESimulator.Core.UI;
using Microsoft.Maui.Controls;

namespace EEE.Components.Common;

[ContentProperty(nameof(WorkspaceContent))]
public partial class EEWorkspaceContainer : ContentView
{
    public static readonly BindableProperty WorkspaceContentProperty =
        BindableProperty.Create(nameof(WorkspaceContent), typeof(View), typeof(EEWorkspaceContainer), null,
            propertyChanged: (b, o, n) => ((EEWorkspaceContainer)b).ViewportHost.Content = (View)n);

    public WorkspaceNode Node { get; }

    public View? WorkspaceContent
    {
        get => (View?)GetValue(WorkspaceContentProperty);
        set => SetValue(WorkspaceContentProperty, value);
    }

    public EEWorkspaceContainer()
    {
        InitializeComponent();
        Node = new WorkspaceNode();
    }
}
