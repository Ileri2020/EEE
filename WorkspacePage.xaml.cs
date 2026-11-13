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
        MainWorkspace.SchematicCanvas.LogicInputToggled += (_, comp) =>
            vm.OnLogicInputToggled(comp);
        MainWorkspace.SchematicCanvas.ConnectionSelected += (_, connection) =>
            vm.OnConnectionSelected(connection);
        vm.ViewportRecenterRequested += MainWorkspace.SchematicCanvas.RecenterToCircuit;
        vm.ViewportZoomRequested += MainWorkspace.SchematicCanvas.ZoomBy;
        vm.ViewportFitToScreenRequested += MainWorkspace.SchematicCanvas.FitToScreen;
        vm.ViewportComponentPlacementRequested += MainWorkspace.SchematicCanvas.GetVisibleTopRightWorldPosition;
        MainWorkspace.SchematicCanvas.ComponentMoveCommitted += (_, move) =>
        {
            MainWorkspace.SchematicCanvas.InvalidateWireGeometryCache();
            vm.CommitComponentMove(move.Component, move.OldX, move.OldY, move.NewX, move.NewY);
        };

        // Wire pin/node selection event
        MainWorkspace.SchematicCanvas.PinSelected += (_, tuple) =>
            vm.OnPinSelected(tuple.Component, tuple.Pin);

        // Wire completed wire connection → add ConnectionNode to circuit
        MainWorkspace.SchematicCanvas.WireConnected += (_, args) =>
        {
            MainWorkspace.SchematicCanvas.InvalidateWireGeometryCache();
            var (fromComp, fromPin, toComp, toPin) = args;
            vm.ConnectPins(fromComp, fromPin, toComp, toPin);
        };

        // Wire cut wire turning into node
        MainWorkspace.SchematicCanvas.WireCutIntoNode += (_, junc) =>
        {
            MainWorkspace.SchematicCanvas.InvalidateWireGeometryCache();
            vm.StatusText = $"Cut wire → Created node {junc.Reference}";
            vm.SelectedComponent = junc;
        };

        // Wire CircuitChanged to refresh the schematic, waveforms, and selected icon after solver results.
        vm.CircuitChanged += () =>
            Dispatcher.Dispatch(() =>
            {
                MainWorkspace.SchematicCanvas.IsSimulationPlaying = vm.IsSimulationPlaying;
                MainWorkspace.SchematicCanvas.ElectricityAnimPhase = vm.ElectricityAnimPhase;
                MainWorkspace.SchematicCanvas.Invalidate();
                if (vm.IsWaveformDrawerOpen)
                    MainWorkspace.WaveformCanvas.Invalidate();
                if (vm.IsDescriptionSidebarVisible)
                    MainWorkspace.SelectedComponentIcon.Invalidate();
            });

        vm.AnimationFrameRequested += () =>
        {
            MainWorkspace.SchematicCanvas.IsSimulationPlaying = vm.IsSimulationPlaying;
            MainWorkspace.SchematicCanvas.ElectricityAnimPhase = vm.ElectricityAnimPhase;
            MainWorkspace.SchematicCanvas.Invalidate();
        };

        // Initialize ActiveTool
        MainWorkspace.SchematicCanvas.SetActiveTool(vm.ActiveTool);

        vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(EEWorkspaceViewModel.ActiveTool))
            {
                MainWorkspace.SchematicCanvas.SetActiveTool(vm.ActiveTool);
            }
        };
    }
}
