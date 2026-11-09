using EEE.ViewModels;
using EESimulator.Core.Models;
using EESimulator.Core.Components.Electrical;

namespace EEE;

public partial class WorkspacePage : ContentPage
{
    private readonly EEWorkspaceViewModel _vm;

    public WorkspacePage(EEWorkspaceViewModel vm)
    {
        _vm = vm;
        InitializeComponent();
        BindingContext = vm;

        // Wire canvas selection events to ViewModel
        MainWorkspace.SchematicCanvas.ComponentSelected += (_, comp) =>
            vm.OnComponentSelected(comp);
        MainWorkspace.SchematicCanvas.ConnectionSelected += (_, connection) =>
            vm.OnConnectionSelected(connection);
        vm.ViewportRecenterRequested += MainWorkspace.SchematicCanvas.RecenterToCircuit;
        MainWorkspace.SchematicCanvas.ComponentMoveCommitted += (_, move) =>
            vm.CommitComponentMove(move.Component, move.OldX, move.OldY, move.NewX, move.NewY);

        // Wire pin/node selection event
        MainWorkspace.SchematicCanvas.PinSelected += (_, tuple) =>
            vm.OnPinSelected(tuple.Component, tuple.Pin);

        // Wire right-click / long-press context event to open Edit Dialog
        MainWorkspace.SchematicCanvas.ComponentContextRequested += (_, comp) =>
            vm.OpenEditDialog(comp);
        MainWorkspace.SchematicCanvas.ComponentLongPressRequested += async (_, comp) =>
        {
            vm.SelectedComponent = comp;
            var action = await DisplayActionSheetAsync(
                comp.Reference,
                "Cancel",
                "Delete",
                "Properties",
                "Duplicate",
                "Rotate 90°");
            switch (action)
            {
                case "Properties":
                    vm.OpenEditDialog(comp);
                    break;
                case "Duplicate":
                    vm.CopySelectedComponent();
                    break;
                case "Rotate 90°":
                    vm.RotateSelectedComponentCommand.Execute(null);
                    break;
                case "Delete":
                    vm.DeleteSelectedComponent();
                    break;
            }
        };

        // Wire rotate handle click → rotate component and redraw
        MainWorkspace.SchematicCanvas.ComponentRotateRequested += (_, comp) =>
        {
            vm.SelectedComponent = comp;
            vm.RotateSelectedComponentCommand.Execute(null);
        };


        // Wire completed wire connection → add ConnectionNode to circuit
        MainWorkspace.SchematicCanvas.WireConnected += (_, args) =>
        {
            var (fromComp, fromPin, toComp, toPin) = args;
            vm.ConnectPins(fromComp, fromPin, toComp, toPin);
        };

        // Wire cut wire turning into node
        MainWorkspace.SchematicCanvas.WireCutIntoNode += (_, junc) =>
        {
            vm.StatusText = $"Cut wire → Created node {junc.Reference}";
            vm.SelectedComponent = junc;
        };

        // Wire CircuitChanged so the canvas redraws immediately when
        // a component is added from the sidebar palette or animation ticks
        vm.CircuitChanged += () =>
            Dispatcher.Dispatch(() =>
            {
                MainWorkspace.SchematicCanvas.IsSimulationPlaying = vm.IsSimulationPlaying;
                MainWorkspace.SchematicCanvas.ElectricityAnimPhase = vm.ElectricityAnimPhase;
                MainWorkspace.SchematicCanvas.Invalidate();
                MainWorkspace.WaveformCanvas.Invalidate();
                MainWorkspace.SelectedComponentIcon.Invalidate();
            });

        // Initialize ActiveTool
        MainWorkspace.SchematicCanvas.ActiveTool = vm.ActiveTool;

        // Sync IsMobilePanMode property from ViewModel to viewport
        vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(EEWorkspaceViewModel.IsMobilePanMode))
            {
                MainWorkspace.SchematicCanvas.IsMobilePanMode = vm.IsMobilePanMode;
                MainWorkspace.SchematicCanvas.Invalidate();

                // Update button visual states for Pan/Select mode
                Dispatcher.Dispatch(() =>
                {
                    MainWorkspace.BtnMobilePan.BackgroundColor    = vm.IsMobilePanMode
                        ? Color.FromArgb("#1C4A2B") : Colors.Transparent;
                    MainWorkspace.BtnMobileSelect.BackgroundColor = !vm.IsMobilePanMode
                        ? Color.FromArgb("#1C3060") : Colors.Transparent;
                });
            }
            else if (e.PropertyName == nameof(EEWorkspaceViewModel.ActiveTool))
            {
                MainWorkspace.SchematicCanvas.ActiveTool = vm.ActiveTool;
            }
        };

        // Apply default mobile mode highlight on load
        MainWorkspace.BtnMobilePan.BackgroundColor    = Color.FromArgb("#1C4A2B");
        MainWorkspace.BtnMobileSelect.BackgroundColor = Colors.Transparent;
    }
}
