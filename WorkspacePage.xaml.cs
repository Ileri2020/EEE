using EEE.ViewModels;
using EESimulator.Core.Models;
using EESimulator.Core.Components.Electrical;
using EESimulator.Commands.Commands;

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
        SchematicCanvas.ComponentSelected += (_, comp) =>
            vm.OnComponentSelected(comp);
        vm.ViewportRecenterRequested += SchematicCanvas.RecenterToCircuit;
        SchematicCanvas.ComponentMoveCommitted += (_, move) =>
            vm.CommitComponentMove(move.Component, move.OldX, move.OldY, move.NewX, move.NewY);

        // Wire pin/node selection event
        SchematicCanvas.PinSelected += (_, tuple) =>
            vm.OnPinSelected(tuple.Component, tuple.Pin);

        // Wire right-click / long-press context event to open Edit Dialog
        SchematicCanvas.ComponentContextRequested += (_, comp) =>
            vm.OpenEditDialog(comp);
        SchematicCanvas.ComponentLongPressRequested += async (_, comp) =>
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
        SchematicCanvas.ComponentRotateRequested += (_, comp) =>
        {
            vm.SelectedComponent = comp;
            vm.RotateSelectedComponentCommand.Execute(null);
        };


        // Wire completed wire connection → add ConnectionNode to circuit
        SchematicCanvas.WireConnected += (_, args) =>
        {
            var (fromComp, fromPin, toComp, toPin) = args;
            try
            {
                var conn = new ConnectionNode(
                    fromComp.Id, fromPin.Id,
                    toComp.Id, toPin.Id);
                vm.Circuit.AddChild(conn);
                vm.StatusText = $"Connected {fromComp.Reference}:{fromPin.Name} → {toComp.Reference}:{toPin.Name}";
            }
            catch (Exception ex)
            {
                vm.StatusText = $"Wire error: {ex.Message}";
            }
        };

        // Wire cut wire turning into node
        SchematicCanvas.WireCutIntoNode += (_, junc) =>
        {
            vm.StatusText = $"Cut wire → Created node {junc.Reference}";
            vm.SelectedComponent = junc;
        };

        // Wire CircuitChanged so the canvas redraws immediately when
        // a component is added from the sidebar palette or animation ticks
        vm.CircuitChanged += () =>
            Dispatcher.Dispatch(() =>
            {
                SchematicCanvas.IsSimulationPlaying = vm.IsSimulationPlaying;
                SchematicCanvas.ElectricityAnimPhase = vm.ElectricityAnimPhase;
                SchematicCanvas.Invalidate();
            });

        // Initialize ActiveTool
        SchematicCanvas.ActiveTool = vm.ActiveTool;

        // Sync IsMobilePanMode property from ViewModel to viewport
        vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(EEWorkspaceViewModel.IsMobilePanMode))
            {
                SchematicCanvas.IsMobilePanMode = vm.IsMobilePanMode;
                SchematicCanvas.Invalidate();

                // Update button visual states for Pan/Select mode
                Dispatcher.Dispatch(() =>
                {
                    BtnMobilePan.BackgroundColor    = vm.IsMobilePanMode
                        ? Color.FromArgb("#1C4A2B") : Colors.Transparent;
                    BtnMobileSelect.BackgroundColor = !vm.IsMobilePanMode
                        ? Color.FromArgb("#1C3060") : Colors.Transparent;
                });
            }
            else if (e.PropertyName == nameof(EEWorkspaceViewModel.ActiveTool))
            {
                SchematicCanvas.ActiveTool = vm.ActiveTool;
            }
        };

        // Apply default mobile mode highlight on load
        BtnMobilePan.BackgroundColor    = Color.FromArgb("#1C4A2B");
        BtnMobileSelect.BackgroundColor = Colors.Transparent;
    }
}
