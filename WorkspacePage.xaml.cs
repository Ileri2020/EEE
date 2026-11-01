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

        // Wire pin/node selection event
        SchematicCanvas.PinSelected += (_, tuple) =>
            vm.OnPinSelected(tuple.Component, tuple.Pin);

        // Wire right-click / long-press context event to open Edit Dialog
        SchematicCanvas.ComponentContextRequested += (_, comp) =>
            vm.OpenEditDialog(comp);

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

        // Wire CircuitChanged so the canvas redraws immediately when
        // a component is added from the sidebar palette or animation ticks
        vm.CircuitChanged += () =>
            Dispatcher.Dispatch(() =>
            {
                SchematicCanvas.IsSimulationPlaying = vm.IsSimulationPlaying;
                SchematicCanvas.ElectricityAnimPhase = vm.ElectricityAnimPhase;
                SchematicCanvas.Invalidate();
            });

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
        };

        // Apply default mobile mode highlight on load
        BtnMobilePan.BackgroundColor    = Color.FromArgb("#1C4A2B");
        BtnMobileSelect.BackgroundColor = Colors.Transparent;
    }
}
