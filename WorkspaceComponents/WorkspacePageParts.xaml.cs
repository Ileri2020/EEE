using Microsoft.Maui.Controls;
using EESimulator.App.Views;

namespace EEE.WorkspaceComponents;

public partial class WorkspaceMenuBar : ContentView
{
    public WorkspaceMenuBar() => InitializeComponent();
}

public partial class WorkspaceFileMenu : ContentView
{
    public WorkspaceFileMenu() => InitializeComponent();
}

public partial class WorkspaceMainLayout : ContentView
{
    public WorkspaceMainLayout() => InitializeComponent();
}

public partial class WorkspaceStatusBar : ContentView
{
    public WorkspaceStatusBar() => InitializeComponent();
}

public partial class WorkspaceEditDialog : ContentView
{
    public WorkspaceEditDialog() => InitializeComponent();
}

public partial class WorkspaceHelpDialog : ContentView
{
    public WorkspaceHelpDialog() => InitializeComponent();
}

public partial class WorkspaceSimulationSettingsDialog : ContentView
{
    public WorkspaceSimulationSettingsDialog() => InitializeComponent();
}

public partial class WorkspaceNetlistDialog : ContentView
{
    public WorkspaceNetlistDialog() => InitializeComponent();
}

public partial class WorkspaceSaveDialog : ContentView
{
    public WorkspaceSaveDialog() => InitializeComponent();
}

public partial class WorkspaceSimulationEventChat : ContentView
{
    public WorkspaceSimulationEventChat() => InitializeComponent();
}

public partial class WorkspaceSimulationEventDialog : ContentView
{
    public WorkspaceSimulationEventDialog() => InitializeComponent();
}

public partial class WorkspaceCreateCircuitComponentDialog : ContentView
{
    public WorkspaceCreateCircuitComponentDialog() => InitializeComponent();
}

public partial class WorkspaceLeftToolbar : ContentView
{
    public WorkspaceLeftToolbar() => InitializeComponent();
}

public partial class WorkspaceControlsSidebar : ContentView
{
    public WorkspaceControlsSidebar() => InitializeComponent();
}

public partial class WorkspaceCenterViewport : ContentView
{
    public WorkspaceCenterViewport() => InitializeComponent();
}

public partial class WorkspaceComponentSidebar : ContentView
{
    public WorkspaceComponentSidebar() => InitializeComponent();
}

public partial class WorkspaceSidebarTrigger : ContentView
{
    public WorkspaceSidebarTrigger() => InitializeComponent();
}

public partial class WorkspaceDescriptionSidebar : ContentView
{
    public WorkspaceDescriptionSidebar() => InitializeComponent();
}

public partial class WorkspaceCenterViewport
{
    public SchematicViewport SchematicCanvasControl => SchematicCanvas;
    public WaveformViewport WaveformCanvasControl => WaveformCanvas;
}

public partial class WorkspaceLeftToolbar
{
    public Button MobilePanButton => BtnMobilePan;
    public Button MobileSelectButton => BtnMobileSelect;
}

public partial class WorkspaceDescriptionSidebar
{
    public ComponentIconView SelectedIcon => SelectedComponentIcon;
}

public partial class WorkspaceMainLayout
{
    public SchematicViewport SchematicCanvas => CenterView.SchematicCanvasControl;
    public WaveformViewport WaveformCanvas => CenterView.WaveformCanvasControl;
    public ComponentIconView SelectedComponentIcon => DescriptionSidebar.SelectedIcon;
    public Button BtnMobilePan => LeftToolbar.MobilePanButton;
    public Button BtnMobileSelect => LeftToolbar.MobileSelectButton;
}
