using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EESimulator.Commands;
using EESimulator.Commands.Commands;
using EESimulator.Core.Models;
using EESimulator.Core.Components.Electrical;
using EESimulator.Core.Components.General;
using EESimulator.Core.UI;
using EESimulator.Scripting;
using EESimulator.Scripting.API;
using EESimulator.Engine;
using EESimulator.Engine.Simulation;
using EESimulator.Formats;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EEE.ViewModels;

/// <summary>
/// Main workspace ViewModel. Owns the circuit document, command manager,
/// Python API, and bidirectional sync between the schematic and script viewports.
/// </summary>
public partial class EEWorkspaceViewModel : ObservableObject
{
    // ── Core objects ─────────────────────────────────────────────────────────
    public CircuitNode Circuit { get; } = new CircuitNode { Name = "Untitled Circuit" };
    public CommandManager CommandManager { get; } = new CommandManager();
    public UIRootNode UI { get; } = new UIRootNode();

    private readonly SimulationManager _simManager;
    public PythonApi PythonApi { get; }
    private readonly ScriptSynchronizer _sync;

    // ── Observable state ─────────────────────────────────────────────────────
    [ObservableProperty]
    public partial string ScriptText { get; set; } =
        "# EESimulator Python Console\n" +
        "# Schematic actions auto-generate code here.\n" +
        "# You can also type commands, e.g.:\n" +
        "#   eesim.add_component('Resistor', 'R1', 100, 150, 1000)\n\n";

    [ObservableProperty]
    public partial string ConsoleOutput { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsSimulating { get; set; }

    [ObservableProperty]
    public partial bool IsScriptViewActive { get; set; } = false;

    [ObservableProperty]
    public partial string StatusText { get; set; } = "Ready";

    [ObservableProperty]
    public partial string ActiveComponentInfo { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string ProjectName { get; set; } = "Untitled.eesim";

    [ObservableProperty]
    public partial bool CanUndo { get; set; }

    [ObservableProperty]
    public partial bool CanRedo { get; set; }

    [ObservableProperty]
    public partial string ActiveTool { get; set; } = "Select";

    [ObservableProperty]
    public partial bool IsSidebarVisible { get; set; } = true;

    [ObservableProperty]
    public partial bool IsControlSidebarVisible { get; set; } = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PlayPauseButtonText))]
    public partial bool IsSimulationPlaying { get; set; } = false;

    public string PlayPauseButtonText => IsSimulationPlaying ? "⏸ Pause" : "▶ Run Sim";

    [ObservableProperty]
    public partial float ElectricityAnimPhase { get; set; } = 0f;

    public ObservableCollection<EEE.Services.CustomControlButton> CustomControlButtons =>
        EEE.Services.ControlSidebarService.CustomButtons;

    // Component Edit Dialog state
    [ObservableProperty]
    public partial bool IsEditDialogVisible { get; set; } = false;

    [ObservableProperty]
    public partial ComponentNode? EditingComponent { get; set; }

    [ObservableProperty]
    public partial string EditName { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string EditType { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string EditParamName { get; set; } = "Value";

    [ObservableProperty]
    public partial double EditParamValue { get; set; } = 0.0;

    [ObservableProperty]
    public partial string EditWireResistance { get; set; } = "0";

    [ObservableProperty]
    public partial bool EditShowLiveProbe { get; set; } = false;

    [ObservableProperty]
    public partial bool EditShowWaveform { get; set; } = false;

    // Tabs for Component Edit Dialog: "General", "Waveform", "Advanced", "Pins"
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEditTabGeneral))]
    [NotifyPropertyChangedFor(nameof(IsEditTabWaveform))]
    [NotifyPropertyChangedFor(nameof(IsEditTabAdvanced))]
    [NotifyPropertyChangedFor(nameof(IsEditTabPins))]
    public partial string SelectedEditTab { get; set; } = "General";

    public bool IsEditTabGeneral => SelectedEditTab == "General";
    public bool IsEditTabWaveform => SelectedEditTab == "Waveform";
    public bool IsEditTabAdvanced => SelectedEditTab == "Advanced";
    public bool IsEditTabPins => SelectedEditTab == "Pins";

    [RelayCommand]
    public void SelectEditTab(string tab) => SelectedEditTab = tab;

    // Classification flags for conditional UI in Edit Dialog
    [ObservableProperty] public partial bool IsSourceComponent { get; set; } = false;
    [ObservableProperty] public partial bool IsBjtComponent { get; set; } = false;
    [ObservableProperty] public partial bool IsMosfetComponent { get; set; } = false;
    [ObservableProperty] public partial bool IsDiodeComponent { get; set; } = false;
    [ObservableProperty] public partial bool IsOpAmpComponent { get; set; } = false;
    [ObservableProperty] public partial bool IsMutualInductanceComponent { get; set; } = false;
    [ObservableProperty] public partial bool IsResistorComponent { get; set; } = false;
    [ObservableProperty] public partial bool IsCapacitorComponent { get; set; } = false;
    [ObservableProperty] public partial bool IsInductorComponent { get; set; } = false;
    [ObservableProperty] public partial bool IsWireComponent { get; set; } = false;

    // AC & Waveform properties
    [ObservableProperty] public partial string EditWaveformType { get; set; } = "DC";
    [ObservableProperty] public partial double EditAcMagnitude { get; set; } = 0.0;
    [ObservableProperty] public partial double EditAcPhase { get; set; } = 0.0;
    [ObservableProperty] public partial double EditSineOffset { get; set; } = 0.0;
    [ObservableProperty] public partial double EditSineAmplitude { get; set; } = 5.0;
    [ObservableProperty] public partial double EditSineFrequency { get; set; } = 1000.0;
    [ObservableProperty] public partial double EditSineDelay { get; set; } = 0.0;
    [ObservableProperty] public partial double EditSineTheta { get; set; } = 0.0;
    [ObservableProperty] public partial double EditSinePhase { get; set; } = 0.0;
    [ObservableProperty] public partial double EditPulseInitial { get; set; } = 0.0;
    [ObservableProperty] public partial double EditPulsePulsed { get; set; } = 5.0;
    [ObservableProperty] public partial double EditPulseDelay { get; set; } = 0.0;
    [ObservableProperty] public partial double EditPulseRiseTime { get; set; } = 1e-6;
    [ObservableProperty] public partial double EditPulseFallTime { get; set; } = 1e-6;
    [ObservableProperty] public partial double EditPulseWidth { get; set; } = 0.5e-3;
    [ObservableProperty] public partial double EditPulsePeriod { get; set; } = 1e-3;

    // Model & Parasitic properties
    [ObservableProperty] public partial double EditTemperature { get; set; } = 27.0;
    [ObservableProperty] public partial double EditTempCoeff1 { get; set; } = 0.0;
    [ObservableProperty] public partial double EditTempCoeff2 { get; set; } = 0.0;
    [ObservableProperty] public partial double EditInitialCondition { get; set; } = 0.0;
    [ObservableProperty] public partial double EditEquivalentSeriesResistance { get; set; } = 0.0;
    [ObservableProperty] public partial double EditDcResistance { get; set; } = 0.0;
    [ObservableProperty] public partial double EditSaturationCurrent { get; set; } = 1e-14;
    [ObservableProperty] public partial double EditEmissionCoefficient { get; set; } = 1.0;
    [ObservableProperty] public partial double EditBreakdownVoltage { get; set; } = 0.0;
    [ObservableProperty] public partial double EditSeriesResistance { get; set; } = 0.0;
    [ObservableProperty] public partial double EditJunctionCapacitance { get; set; } = 0.0;
    [ObservableProperty] public partial double EditGainBeta { get; set; } = 100.0;
    [ObservableProperty] public partial double EditEarlyVoltage { get; set; } = 0.0;
    [ObservableProperty] public partial string EditBjtPolarity { get; set; } = "NPN";
    [ObservableProperty] public partial string EditMosfetChannel { get; set; } = "NChannel";
    [ObservableProperty] public partial double EditThresholdVoltage { get; set; } = 2.0;
    [ObservableProperty] public partial double EditTransconductance { get; set; } = 2e-5;
    [ObservableProperty] public partial double EditChannelWidth { get; set; } = 1e-4;
    [ObservableProperty] public partial double EditChannelLength { get; set; } = 1e-6;
    [ObservableProperty] public partial double EditLambda { get; set; } = 0.0;
    [ObservableProperty] public partial double EditOpenLoopGain { get; set; } = 100000.0;
    [ObservableProperty] public partial double EditGainBandwidth { get; set; } = 1e6;
    [ObservableProperty] public partial double EditSlewRate { get; set; } = 0.5;
    [ObservableProperty] public partial double EditSupplyPositive { get; set; } = 15.0;
    [ObservableProperty] public partial double EditSupplyNegative { get; set; } = -15.0;
    [ObservableProperty] public partial string EditInductor1 { get; set; } = "L1";
    [ObservableProperty] public partial string EditInductor2 { get; set; } = "L2";
    [ObservableProperty] public partial double EditCoupling { get; set; } = 0.99;

    public ObservableCollection<PinEditItem> EditPins { get; } = new();

    // Simulation Setup modal state
    [ObservableProperty] public partial bool IsSimSettingsVisible { get; set; } = false;
    [ObservableProperty] public partial string SelectedAnalysisType { get; set; } = "OperatingPoint";
    [ObservableProperty] public partial double SimTransientStartTime { get; set; } = 0.0;
    [ObservableProperty] public partial double SimTransientStopTime { get; set; } = 0.01;
    [ObservableProperty] public partial double SimTransientStepTime { get; set; } = 1e-5;
    [ObservableProperty] public partial string SimAcSweepType { get; set; } = "Decade";
    [ObservableProperty] public partial int SimAcPoints { get; set; } = 10;
    [ObservableProperty] public partial double SimAcStartFrequency { get; set; } = 1.0;
    [ObservableProperty] public partial double SimAcStopFrequency { get; set; } = 1e5;
    [ObservableProperty] public partial string SimDcSweepSource { get; set; } = "V1";
    [ObservableProperty] public partial double SimDcStart { get; set; } = 0.0;
    [ObservableProperty] public partial double SimDcStop { get; set; } = 5.0;
    [ObservableProperty] public partial double SimDcStep { get; set; } = 0.1;

    [RelayCommand]
    public void SetSelectedAnalysisType(string type) => SelectedAnalysisType = type;

    // SPICE Netlist export preview
    [ObservableProperty] public partial string GeneratedSpiceNetlist { get; set; } = string.Empty;
    [ObservableProperty] public partial bool IsNetlistModalVisible { get; set; } = false;

    // Waveform Drawer state
    [ObservableProperty]
    public partial bool IsWaveformDrawerOpen { get; set; } = false;

    [ObservableProperty]
    public partial ComponentNode? WaveformComponent { get; set; }

    // Help modal state
    [ObservableProperty]
    public partial bool IsHelpVisible { get; set; } = false;

    // Mobile interaction mode: Pan (default on mobile) or Select
    [ObservableProperty]
    public partial bool IsMobilePanMode { get; set; } = true;

    // Component palette items for the sidebar (Electrical, Electronics, General)
    public ObservableCollection<ComponentPaletteItem> ComponentPalette { get; } = new()
    {
        // ── Electrical (Passive & Sources) ───────────────────────────────────
        new ComponentPaletteItem("Resistor",      "R",   "Ω",  "#F78166", "Electrical",  "Linear resistor",       "resistor.svg"),
        new ComponentPaletteItem("Capacitor",     "C",   "F",  "#79C0FF", "Electrical",  "Capacitor",             "capacitor.svg"),
        new ComponentPaletteItem("Inductor",      "L",   "H",  "#D2A8FF", "Electrical",  "Inductor",              "inductor.svg"),
        new ComponentPaletteItem("VoltageSource", "V",   "V",  "#56D364", "Electrical",  "Voltage source",        "voltagesource.svg"),
        new ComponentPaletteItem("CurrentSource", "I",   "A",  "#56D364", "Electrical",  "Current source",        "currentsource.svg"),
        new ComponentPaletteItem("Ground",        "⏚",   "GND","#8B949E", "Electrical",  "Circuit ground",        "ground.svg"),
        new ComponentPaletteItem("Diode",            "D",   "▷|", "#FF7B72", "Electrical",  "Semiconductor diode",      "diode.svg"),
        new ComponentPaletteItem("MutualInductance", "K",   "k",  "#D2A8FF", "Electrical",  "Mutual coupled inductors", "mutualinductance.svg"),

        // ── Electronics (Semiconductors & Active) ────────────────────────────
        new ComponentPaletteItem("LED",           "💡",  "LED","#E3B341", "Electronics", "Light Emitting Diode",  "led.svg"),
        new ComponentPaletteItem("BJT",           "Q",   "BJT","#FFA657", "Electronics", "BJT NPN/PNP",           "bjt.svg"),
        new ComponentPaletteItem("MOSFET",        "M",   "MOS","#D2A8FF", "Electronics", "MOSFET Field-Effect",   "mosfet.svg"),
        new ComponentPaletteItem("OpAmp",         "▷",   "AMP","#79C0FF", "Electronics", "Operational Amplifier", "opamp.svg"),
        new ComponentPaletteItem("AND",           "&",   "AND","#58A6FF", "Electronics", "2-Input AND Gate",      "and.svg"),
        new ComponentPaletteItem("OR",            "≥1",  "OR", "#58A6FF", "Electronics", "2-Input OR Gate",       "or.svg"),
        new ComponentPaletteItem("NOT",           "¬",   "NOT","#58A6FF", "Electronics", "Inverter / NOT Gate",   "not.svg"),
        new ComponentPaletteItem("NAND",          "⊼",   "NND","#58A6FF", "Electronics", "2-Input NAND Gate",     "nand.svg"),
        new ComponentPaletteItem("NOR",           "⊽",   "NOR","#58A6FF", "Electronics", "2-Input NOR Gate",      "nor.svg"),
        new ComponentPaletteItem("XOR",           "=1",  "XOR","#58A6FF", "Electronics", "2-Input XOR Gate",      "xor.svg"),
        new ComponentPaletteItem("XNOR",          "⊙",   "XNR","#58A6FF", "Electronics", "2-Input XNOR Gate",     "xnor.svg"),
        new ComponentPaletteItem("LogicGate",     "⊳",   "GATE","#58A6FF", "Electronics", "Digital Logic Gate",      "logicgate.svg"),
        new ComponentPaletteItem("IC",            "⊞",   "IC", "#8B949E", "Electronics", "Integrated Circuit",    "ic.svg"),

        // ── General (Structural & Schematic) ─────────────────────────────────
        new ComponentPaletteItem("Wire",          "—",   "W",  "#58A6FF", "General", "Wire segment",         "wire.svg"),
        new ComponentPaletteItem("Junction",      "•",   "J",  "#58A6FF", "General", "Wire junction",        "junction.svg"),
        new ComponentPaletteItem("Label",         "🏷",  "LBL","#56D364", "General", "Net label",            "label.svg"),
        new ComponentPaletteItem("TextAnnotation","🗎",  "TXT","#8B949E", "General", "Text note",            "textannotation.svg"),
        new ComponentPaletteItem("Connector",     "⑉",   "HDR","#E3B341", "General", "Header connector",     "connector.svg")
    };

    /// <summary>Raised when the circuit changes so the canvas can Invalidate immediately.</summary>
    public event Action? CircuitChanged;

    // ── Constructor ──────────────────────────────────────────────────────────
    public EEWorkspaceViewModel()
    {
        _simManager = new SimulationManager();
        PythonApi   = new PythonApi(CommandManager, Circuit, _simManager, UI);
        _sync       = new ScriptSynchronizer(CommandManager, Circuit);

        // Register default UI nodes under RootNode hierarchy
        UI.Workspace.Title = "Schematic Workspace";
        var paletteSidebar = new SidebarNode("ComponentPalette", "Left");
        var editDialog = new DialogNode("ComponentEditDialog", "Edit Component");
        var runButton = new ButtonNode("RunSim", "Run Simulation", () => _ = SimulateAsync());
        var waveButton = new ButtonNode("ToggleWaveform", "Toggle Waveform", () => ToggleWaveformDrawer());

        UI.Workspace.AddChild(paletteSidebar);
        UI.Workspace.AddChild(editDialog);
        UI.Workspace.AddChild(runButton);
        UI.Workspace.AddChild(waveButton);

        // Sync script viewport when schematic commands run
        _sync.ScriptUpdated += script =>
        {
            // Merge generated script lines into our displayed script
            ScriptText = script;
        };

        // History state
        CommandManager.CommandExecuted += (_, e) =>
        {
            CanUndo    = CommandManager.CanUndo;
            CanRedo    = CommandManager.CanRedo;
            StatusText = $"✓ {e.Command.Description}";
        };
        CommandManager.CommandUndone += (_, _2) =>
        {
            CanUndo = CommandManager.CanUndo;
            CanRedo = CommandManager.CanRedo;
            StatusText = "Undone";
        };
        CommandManager.CommandRedone += (_, e) =>
        {
            CanUndo    = CommandManager.CanUndo;
            CanRedo    = CommandManager.CanRedo;
            StatusText = $"Redone: {e.Command.Description}";
        };

        // Python API events
        PythonApi.OnWindowOpenRequested   += name => StatusText = $"Open: {name}";
        PythonApi.OnActiveViewportChanged += name => StatusText = $"Viewport: {name}";
        PythonApi.OnControlButtonRegistered += (label, act) => EEE.Services.ControlSidebarService.AddButton(label, act);
        PythonApi.OnActiveToolChanged += tool => SelectTool(tool);
    }

    // ── Commands ─────────────────────────────────────────────────────────────
    [RelayCommand]
    private void SelectTool(string tool) => ActiveTool = tool;

    [RelayCommand]
    private void ToggleHelp() => IsHelpVisible = !IsHelpVisible;

    [RelayCommand]
    private async Task OpenSettings()
    {
        try
        {
            await Shell.Current.GoToAsync("settings");
        }
        catch
        {
            StatusText = "Settings opened";
        }
    }

    [RelayCommand]
    private void SetMobilePanMode()
    {
        IsMobilePanMode = true;
        ActiveTool = "Pan";
        StatusText = "Mobile: Pan mode active";
    }

    [RelayCommand]
    private void SetMobileSelectMode()
    {
        IsMobilePanMode = false;
        ActiveTool = "Select";
        StatusText = "Mobile: Select mode active";
    }

    [RelayCommand]
    private void Undo()
    {
        CommandManager.Undo();
        StatusText = "Undo";
    }

    [RelayCommand]
    private void Redo()
    {
        CommandManager.Redo();
        StatusText = "Redo";
    }

    [RelayCommand]
    private void ToggleViewport() => IsScriptViewActive = !IsScriptViewActive;

    [RelayCommand]
    private void ToggleSidebar()
    {
        IsSidebarVisible = !IsSidebarVisible;
        if (IsSidebarVisible && DeviceInfo.Idiom == DeviceIdiom.Phone)
            IsControlSidebarVisible = false;
    }

    [RelayCommand]
    private void ToggleControlSidebar()
    {
        IsControlSidebarVisible = !IsControlSidebarVisible;
        if (IsControlSidebarVisible && DeviceInfo.Idiom == DeviceIdiom.Phone)
            IsSidebarVisible = false;
    }

    private Microsoft.Maui.Dispatching.IDispatcherTimer? _animTimer;

    [RelayCommand]
    public void ToggleSimulationPlayback()
    {
        IsSimulationPlaying = !IsSimulationPlaying;
        if (IsSimulationPlaying)
        {
            if (_animTimer == null)
            {
                _animTimer = Application.Current?.Dispatcher?.CreateTimer() ?? Microsoft.Maui.Dispatching.Dispatcher.GetForCurrentThread()?.CreateTimer();
                if (_animTimer != null)
                {
                    _animTimer.Interval = TimeSpan.FromMilliseconds(40);
                    _animTimer.Tick += (s, e) =>
                    {
                        if (IsSimulationPlaying)
                        {
                            ElectricityAnimPhase = (ElectricityAnimPhase + 0.05f) % 1.0f;
                            CircuitChanged?.Invoke();
                        }
                    };
                }
            }
            _animTimer?.Start();
            StatusText = "▶ Simulation Running: Electricity flowing along circuits";
        }
        else
        {
            _animTimer?.Stop();
            StatusText = "⏸ Simulation Paused";
        }
        CircuitChanged?.Invoke();
    }

    [RelayCommand]
    public void ExecuteCustomAction(EEE.Services.CustomControlButton button)
    {
        if (button == null) return;
        var act = button.ActionName;

        switch (act)
        {
            case "Play / Pause Simulation":
                ToggleSimulationPlayback();
                break;
            case "Run Operating Point":
                _ = SimulateAsync();
                break;
            case "Pan Mode":
                SetMobilePanMode();
                break;
            case "Select Mode":
                SetMobileSelectMode();
                break;
            case "Component Edit Dialog":
                if (EditingComponent != null)
                    OpenEditDialog(EditingComponent);
                else
                {
                    var firstComp = Circuit.FindChildren<ComponentNode>().FirstOrDefault();
                    if (firstComp != null) OpenEditDialog(firstComp);
                    else StatusText = "Select a component to edit";
                }
                break;
            case "Move Component":
                ActiveTool = "Move";
                StatusText = "Move tool active: drag component";
                break;
            case "Draw Wire":
                ActiveTool = "Wire";
                SelectTool("Wire");
                StatusText = "Wire tool active: click or drag pins";
                break;
            case "Zoom In":
                SelectTool("ZoomIn");
                break;
            case "Zoom Out":
                SelectTool("ZoomOut");
                break;
            case "Reset / Fit View":
                SelectTool("FitAll");
                break;
            case "Undo":
                Undo();
                break;
            case "Redo":
                Redo();
                break;
            case "Toggle Component Palette":
                ToggleSidebar();
                break;
            case "Toggle Waveform Drawer":
                ToggleWaveformDrawer();
                break;
            case "Toggle Script / Schematic":
                ToggleViewport();
                break;
            case "Open Help":
                ToggleHelp();
                break;
            case "Open Settings":
                _ = OpenSettings();
                break;
            default:
                StatusText = $"Action [{button.Label}]: {act}";
                break;
        }
    }

    [RelayCommand]
    public void DeleteCustomButton(EEE.Services.CustomControlButton button)
    {
        if (button != null)
        {
            var lbl = button.Label;
            EEE.Services.ControlSidebarService.DeleteButton(button);
            StatusText = $"Disconnected and deleted control button [{lbl}]";
        }
    }

    [RelayCommand]
    private async Task RunScriptAsync()
    {
        try
        {
            StatusText    = "Running script…";
            ConsoleOutput = "";

            foreach (var rawLine in ScriptText.Split('\n'))
            {
                var line = rawLine.Trim();
                if (string.IsNullOrEmpty(line) || line.StartsWith('#'))
                    continue;

                ConsoleOutput += $">>> {line}\n";

                // Parse "eesim.xxx(...)" calls and dispatch to PythonApi
                var result = DispatchApiCall(line);
                if (result != null)
                    ConsoleOutput += result + "\n";
            }

            StatusText = "Script complete ✓";
        }
        catch (Exception ex)
        {
            ConsoleOutput += $"ERROR: {ex.Message}\n";
            StatusText = "Script error";
        }

        await Task.CompletedTask;
    }

    public SimulationSettings BuildSimulationSettings()
    {
        var analysis = SelectedAnalysisType?.ToLowerInvariant() switch
        {
            "transient" => AnalysisType.Transient,
            "ac" => AnalysisType.AC,
            "dcsweep" or "dc sweep" => AnalysisType.DCSweep,
            _ => AnalysisType.OperatingPoint
        };

        return new SimulationSettings
        {
            Analysis = analysis,
            StartTime = SimTransientStartTime,
            StopTime = SimTransientStopTime > SimTransientStartTime ? SimTransientStopTime : SimTransientStartTime + 0.01,
            StepTime = SimTransientStepTime > 0 ? SimTransientStepTime : 1e-5,
            AcSweepType = SimAcSweepType ?? "Decade",
            AcPoints = SimAcPoints > 0 ? SimAcPoints : 10,
            AcStartFrequency = SimAcStartFrequency > 0 ? SimAcStartFrequency : 1.0,
            AcStopFrequency = SimAcStopFrequency > SimAcStartFrequency ? SimAcStopFrequency : 1e5,
            DcSweepSource = string.IsNullOrWhiteSpace(SimDcSweepSource) ? "V1" : SimDcSweepSource.Trim(),
            DcStart = SimDcStart,
            DcStop = SimDcStop,
            DcStep = SimDcStep != 0 ? SimDcStep : 0.1
        };
    }

    [RelayCommand]
    public void OpenSimSettings() => IsSimSettingsVisible = true;

    [RelayCommand]
    public void CloseSimSettings() => IsSimSettingsVisible = false;

    [RelayCommand]
    public async Task RunConfiguredSimulationAsync()
    {
        IsSimSettingsVisible = false;
        await SimulateAsync();
    }

    [RelayCommand]
    public void ViewSpiceNetlist()
    {
        var settings = BuildSimulationSettings();
        GeneratedSpiceNetlist = SpiceNetlistExporter.ExportToSpice(Circuit, settings);
        IsNetlistModalVisible = true;
    }

    [RelayCommand]
    public void CloseSpiceNetlist() => IsNetlistModalVisible = false;

    [RelayCommand]
    public async Task CopySpiceNetlistAsync()
    {
        if (!string.IsNullOrEmpty(GeneratedSpiceNetlist))
        {
            await Clipboard.Default.SetTextAsync(GeneratedSpiceNetlist);
            StatusText = "SPICE Netlist copied to clipboard ✓";
        }
    }

    [RelayCommand]
    private async Task SimulateAsync()
    {
        try
        {
            IsSimulating  = true;
            StatusText    = $"Simulating ({SelectedAnalysisType})…";
            ConsoleOutput = "";

            var settings = BuildSimulationSettings();
            var results  = await Task.Run(() => _simManager.Run(Circuit, settings));

            ConsoleOutput = FormatResults(results);
            StatusText    = results.Success
                ? $"{settings.Analysis} simulation complete ✓"
                : $"Sim error: {results.Message}";
        }
        catch (Exception ex)
        {
            ConsoleOutput = $"Simulation failed: {ex.Message}";
            StatusText    = "Simulation error";
        }
        finally
        {
            IsSimulating = false;
        }
    }

    [RelayCommand]
    private void AddComponentToCircuit(ComponentPaletteItem item)
    {
        var name = GetNextReference(item.Type);
        int count = Circuit.FindChildren<ComponentNode>().Count();
        // Place on a grid pattern, offset from centre of view
        double cx = 100 + (count % 6) * 90;
        double cy = 100 + (count / 6) * 90;

        var cmd = new AddComponentCommand(Circuit, item.Type, name, cx, cy, 0)
        {
            Origin = CommandOrigin.Schematic
        };
        CommandManager.ExecuteCommand(cmd);
        StatusText = $"Added {name}";

        // Notify the canvas to redraw immediately
        CircuitChanged?.Invoke();
    }

    [RelayCommand]
    private void ClearConsole() => ConsoleOutput = string.Empty;

    [RelayCommand]
    private void NewCircuit()
    {
        foreach (var child in Circuit.Children.ToList())
            Circuit.RemoveChild(child);

        CommandManager.Clear();
        ScriptText    = "# EESimulator Python Console\n";
        ConsoleOutput = string.Empty;
        StatusText    = "New circuit";
        ProjectName   = "Untitled.eesim";
        CanUndo       = false;
        CanRedo       = false;
    }

    // Called by the canvas when user clicks a component
    public void OnComponentSelected(ComponentNode comp)
    {
        var param = comp.ElectricalParameters.FirstOrDefault();
        string paramStr = param.Key != null ? $"{param.Key}={param.Value:G4}" : "";
        ActiveComponentInfo = $"{comp.Reference}  [{comp.ComponentType}]  ({comp.X:F0}, {comp.Y:F0})  {paramStr}";
        StatusText = $"Selected {comp.Reference} [{comp.ComponentType}]";
    }

    // Called by the canvas when user clicks a specific node/pin on a component
    public void OnPinSelected(ComponentNode comp, ComponentPin pin)
    {
        ActiveComponentInfo = $"{comp.Reference}:{pin.Name} (Pin {pin.Id}) @ ({comp.X + pin.RelativeX:F0}, {comp.Y + pin.RelativeY:F0})";
        StatusText = $"Selected node {comp.Reference}:{pin.Name}";
    }

    public void OpenEditDialog(ComponentNode comp)
    {
        EditingComponent = comp;
        EditName = comp.Reference;
        EditType = comp.ComponentType;
        EditShowLiveProbe = comp.ShowLiveProbe;
        EditShowWaveform = comp.ShowWaveform;
        SelectedEditTab = "General";

        // Reset component type flags
        IsSourceComponent = comp is VoltageSourceComponent || comp is CurrentSourceComponent;
        IsBjtComponent = comp is EESimulator.Core.Components.Electronics.BJTComponent;
        IsMosfetComponent = comp is EESimulator.Core.Components.Electronics.MOSFETComponent;
        IsDiodeComponent = comp is DiodeComponent;
        IsOpAmpComponent = comp is EESimulator.Core.Components.Electronics.OpAmpComponent;
        IsMutualInductanceComponent = comp is MutualInductanceComponent;
        IsResistorComponent = comp is ResistorComponent;
        IsCapacitorComponent = comp is CapacitorComponent;
        IsInductorComponent = comp is InductorComponent;
        IsWireComponent = comp is WireComponent;

        if (comp is ResistorComponent r)
        {
            EditParamName = "Resistance (Ω)";
            EditParamValue = r.Resistance;
            EditTemperature = r.Temperature;
            EditTempCoeff1 = r.TemperatureCoefficient1;
            EditTempCoeff2 = r.TemperatureCoefficient2;
        }
        else if (comp is CapacitorComponent c)
        {
            EditParamName = "Capacitance (F)";
            EditParamValue = c.Capacitance;
            EditInitialCondition = c.InitialCondition;
            EditEquivalentSeriesResistance = c.EquivalentSeriesResistance;
            EditTemperature = c.Temperature;
            EditTempCoeff1 = c.TemperatureCoefficient1;
            EditTempCoeff2 = c.TemperatureCoefficient2;
        }
        else if (comp is InductorComponent l)
        {
            EditParamName = "Inductance (H)";
            EditParamValue = l.Inductance;
            EditInitialCondition = l.InitialCondition;
            EditDcResistance = l.DcResistance;
        }
        else if (comp is VoltageSourceComponent v)
        {
            EditParamName = "DC Voltage (V)";
            EditParamValue = v.Voltage;
            EditAcMagnitude = v.AcMagnitude;
            EditAcPhase = v.AcPhase;
            EditWaveformType = string.IsNullOrWhiteSpace(v.WaveformType) ? "DC" : v.WaveformType;
            EditSineOffset = v.SineOffset;
            EditSineAmplitude = v.SineAmplitude;
            EditSineFrequency = v.SineFrequency;
            EditSineDelay = v.SineDelay;
            EditSineTheta = v.SineTheta;
            EditSinePhase = v.SinePhase;
            EditPulseInitial = v.PulseInitial;
            EditPulsePulsed = v.PulsePulsed;
            EditPulseDelay = v.PulseDelay;
            EditPulseRiseTime = v.PulseRiseTime;
            EditPulseFallTime = v.PulseFallTime;
            EditPulseWidth = v.PulseWidth;
            EditPulsePeriod = v.PulsePeriod;
        }
        else if (comp is CurrentSourceComponent i)
        {
            EditParamName = "DC Current (A)";
            EditParamValue = i.Current;
            EditAcMagnitude = i.AcMagnitude;
            EditAcPhase = i.AcPhase;
            EditWaveformType = string.IsNullOrWhiteSpace(i.WaveformType) ? "DC" : i.WaveformType;
            EditSineOffset = i.SineOffset;
            EditSineAmplitude = i.SineAmplitude;
            EditSineFrequency = i.SineFrequency;
            EditSineDelay = i.SineDelay;
            EditPulseInitial = i.PulseInitial;
            EditPulsePulsed = i.PulsePulsed;
            EditPulseDelay = i.PulseDelay;
            EditPulseRiseTime = i.PulseRiseTime;
            EditPulseFallTime = i.PulseFallTime;
            EditPulseWidth = i.PulseWidth;
            EditPulsePeriod = i.PulsePeriod;
        }
        else if (comp is DiodeComponent d)
        {
            EditParamName = "Saturation Current Is (A)";
            EditParamValue = d.SaturationCurrent;
            EditSaturationCurrent = d.SaturationCurrent;
            EditEmissionCoefficient = d.EmissionCoefficient;
            EditBreakdownVoltage = d.BreakdownVoltage;
            EditSeriesResistance = d.SeriesResistance;
            EditJunctionCapacitance = d.JunctionCapacitance;
        }
        else if (comp is EESimulator.Core.Components.Electronics.BJTComponent bjt)
        {
            EditParamName = "Gain Beta (Bf)";
            EditParamValue = bjt.GainBeta;
            EditGainBeta = bjt.GainBeta;
            EditSaturationCurrent = bjt.SaturationCurrent;
            EditEarlyVoltage = bjt.EarlyVoltage;
            EditBjtPolarity = bjt.Polarity == EESimulator.Core.Components.Electronics.BjtPolarity.PNP ? "PNP" : "NPN";
        }
        else if (comp is EESimulator.Core.Components.Electronics.MOSFETComponent mos)
        {
            EditParamName = "Threshold Voltage Vto (V)";
            EditParamValue = mos.ThresholdVoltage;
            EditThresholdVoltage = mos.ThresholdVoltage;
            EditTransconductance = mos.Transconductance;
            EditChannelWidth = mos.ChannelWidth;
            EditChannelLength = mos.ChannelLength;
            EditLambda = mos.Lambda;
            EditMosfetChannel = mos.Channel == EESimulator.Core.Components.Electronics.MosfetChannel.PChannel ? "PChannel" : "NChannel";
        }
        else if (comp is EESimulator.Core.Components.Electronics.OpAmpComponent op)
        {
            EditParamName = "Open-Loop Gain Aol";
            EditParamValue = op.OpenLoopGain;
            EditOpenLoopGain = op.OpenLoopGain;
            EditGainBandwidth = op.GainBandwidth;
            EditSlewRate = op.SlewRate;
            EditSupplyPositive = op.SupplyPositive;
            EditSupplyNegative = op.SupplyNegative;
        }
        else if (comp is MutualInductanceComponent m)
        {
            EditParamName = "Coupling Factor k";
            EditParamValue = m.Coupling;
            EditCoupling = m.Coupling;
            EditInductor1 = m.Inductor1;
            EditInductor2 = m.Inductor2;
        }
        else if (comp is WireComponent w)
        {
            EditParamName = "Parasitic Resistance (Ω)";
            EditParamValue = w.Resistance;
            EditWireResistance = w.Resistance.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }
        else if (comp.ElectricalParameters.Count > 0)
        {
            var first = comp.ElectricalParameters.First();
            EditParamName = first.Key;
            EditParamValue = first.Value;
        }
        else
        {
            EditParamName = "Parameter";
            EditParamValue = 0;
        }

        EditPins.Clear();
        foreach (var pin in comp.Pins)
        {
            EditPins.Add(new PinEditItem(pin.Id, pin.Name));
        }

        IsEditDialogVisible = true;
    }

    [RelayCommand]
    private void CloseEditDialog()
    {
        IsEditDialogVisible = false;
    }

    [RelayCommand]
    private void ToggleEditLiveProbe()
    {
        EditShowLiveProbe = !EditShowLiveProbe;
        if (EditingComponent != null)
        {
            EditingComponent.ShowLiveProbe = EditShowLiveProbe;
        }
    }

    [RelayCommand]
    private void ToggleEditWaveform()
    {
        EditShowWaveform = !EditShowWaveform;
        if (EditingComponent != null)
        {
            EditingComponent.ShowWaveform = EditShowWaveform;
            if (EditShowWaveform)
            {
                WaveformComponent = EditingComponent;
                IsWaveformDrawerOpen = true;
            }
        }
    }

    [RelayCommand]
    private void ApplyEditDialog()
    {
        if (EditingComponent == null)
        {
            IsEditDialogVisible = false;
            return;
        }

        if (!string.IsNullOrWhiteSpace(EditName))
        {
            EditingComponent.Name = EditName.Trim();
            EditingComponent.Reference = EditName.Trim();
        }

        if (EditingComponent is ResistorComponent r)
        {
            r.Resistance = Math.Max(EditParamValue, 1e-6);
            r.Temperature = EditTemperature;
            r.TemperatureCoefficient1 = EditTempCoeff1;
            r.TemperatureCoefficient2 = EditTempCoeff2;
        }
        else if (EditingComponent is CapacitorComponent c)
        {
            c.Capacitance = Math.Max(EditParamValue, 1e-15);
            c.InitialCondition = EditInitialCondition;
            c.EquivalentSeriesResistance = Math.Max(EditEquivalentSeriesResistance, 0.0);
            c.Temperature = EditTemperature;
            c.TemperatureCoefficient1 = EditTempCoeff1;
            c.TemperatureCoefficient2 = EditTempCoeff2;
        }
        else if (EditingComponent is InductorComponent l)
        {
            l.Inductance = Math.Max(EditParamValue, 1e-15);
            l.InitialCondition = EditInitialCondition;
            l.DcResistance = Math.Max(EditDcResistance, 0.0);
        }
        else if (EditingComponent is VoltageSourceComponent v)
        {
            v.Voltage = EditParamValue;
            v.AcMagnitude = EditAcMagnitude;
            v.AcPhase = EditAcPhase;
            v.WaveformType = string.IsNullOrWhiteSpace(EditWaveformType) ? "DC" : EditWaveformType;
            v.SineOffset = EditSineOffset;
            v.SineAmplitude = EditSineAmplitude;
            v.SineFrequency = Math.Max(EditSineFrequency, 0.1);
            v.SineDelay = EditSineDelay;
            v.SineTheta = EditSineTheta;
            v.SinePhase = EditSinePhase;
            v.PulseInitial = EditPulseInitial;
            v.PulsePulsed = EditPulsePulsed;
            v.PulseDelay = EditPulseDelay;
            v.PulseRiseTime = Math.Max(EditPulseRiseTime, 1e-12);
            v.PulseFallTime = Math.Max(EditPulseFallTime, 1e-12);
            v.PulseWidth = Math.Max(EditPulseWidth, 1e-12);
            v.PulsePeriod = Math.Max(EditPulsePeriod, 1e-12);
        }
        else if (EditingComponent is CurrentSourceComponent i)
        {
            i.Current = EditParamValue;
            i.AcMagnitude = EditAcMagnitude;
            i.AcPhase = EditAcPhase;
            i.WaveformType = string.IsNullOrWhiteSpace(EditWaveformType) ? "DC" : EditWaveformType;
            i.SineOffset = EditSineOffset;
            i.SineAmplitude = EditSineAmplitude;
            i.SineFrequency = Math.Max(EditSineFrequency, 0.1);
            i.SineDelay = EditSineDelay;
            i.PulseInitial = EditPulseInitial;
            i.PulsePulsed = EditPulsePulsed;
            i.PulseDelay = EditPulseDelay;
            i.PulseRiseTime = Math.Max(EditPulseRiseTime, 1e-12);
            i.PulseFallTime = Math.Max(EditPulseFallTime, 1e-12);
            i.PulseWidth = Math.Max(EditPulseWidth, 1e-12);
            i.PulsePeriod = Math.Max(EditPulsePeriod, 1e-12);
        }
        else if (EditingComponent is DiodeComponent d)
        {
            d.SaturationCurrent = EditSaturationCurrent > 0 ? EditSaturationCurrent : Math.Max(EditParamValue, 1e-16);
            d.EmissionCoefficient = Math.Max(EditEmissionCoefficient, 0.1);
            d.BreakdownVoltage = EditBreakdownVoltage;
            d.SeriesResistance = Math.Max(EditSeriesResistance, 0.0);
            d.JunctionCapacitance = Math.Max(EditJunctionCapacitance, 0.0);
        }
        else if (EditingComponent is EESimulator.Core.Components.Electronics.BJTComponent bjt)
        {
            bjt.GainBeta = EditGainBeta > 0 ? EditGainBeta : Math.Max(EditParamValue, 1.0);
            bjt.SaturationCurrent = Math.Max(EditSaturationCurrent, 1e-16);
            bjt.EarlyVoltage = EditEarlyVoltage;
            bjt.Polarity = EditBjtPolarity.Equals("PNP", StringComparison.OrdinalIgnoreCase)
                ? EESimulator.Core.Components.Electronics.BjtPolarity.PNP
                : EESimulator.Core.Components.Electronics.BjtPolarity.NPN;
        }
        else if (EditingComponent is EESimulator.Core.Components.Electronics.MOSFETComponent mos)
        {
            mos.ThresholdVoltage = EditThresholdVoltage != 0 ? EditThresholdVoltage : EditParamValue;
            mos.Transconductance = Math.Max(EditTransconductance, 1e-9);
            mos.ChannelWidth = Math.Max(EditChannelWidth, 1e-9);
            mos.ChannelLength = Math.Max(EditChannelLength, 1e-9);
            mos.Lambda = EditLambda;
            mos.Channel = EditMosfetChannel.Equals("PChannel", StringComparison.OrdinalIgnoreCase)
                ? EESimulator.Core.Components.Electronics.MosfetChannel.PChannel
                : EESimulator.Core.Components.Electronics.MosfetChannel.NChannel;
        }
        else if (EditingComponent is EESimulator.Core.Components.Electronics.OpAmpComponent op)
        {
            op.OpenLoopGain = EditOpenLoopGain > 0 ? EditOpenLoopGain : Math.Max(EditParamValue, 1.0);
            op.GainBandwidth = Math.Max(EditGainBandwidth, 1.0);
            op.SlewRate = Math.Max(EditSlewRate, 0.001);
            op.SupplyPositive = EditSupplyPositive;
            op.SupplyNegative = EditSupplyNegative;
        }
        else if (EditingComponent is MutualInductanceComponent m)
        {
            m.Coupling = Math.Clamp(EditCoupling > 0 ? EditCoupling : EditParamValue, 0.0, 1.0);
            if (!string.IsNullOrWhiteSpace(EditInductor1)) m.Inductor1 = EditInductor1.Trim();
            if (!string.IsNullOrWhiteSpace(EditInductor2)) m.Inductor2 = EditInductor2.Trim();
        }
        else if (EditingComponent is WireComponent w)
        {
            w.Resistance = double.TryParse(EditWireResistance, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var wr) ? wr : Math.Max(EditParamValue, 0);
        }
        else if (!string.IsNullOrEmpty(EditParamName))
        {
            EditingComponent.ElectricalParameters[EditParamName] = EditParamValue;
        }

        // Apply node / pin name changes
        foreach (var pItem in EditPins)
        {
            var targetPin = EditingComponent.Pins.FirstOrDefault(p => p.Id == pItem.Id);
            if (targetPin != null && !string.IsNullOrWhiteSpace(pItem.Name))
            {
                targetPin.Name = pItem.Name.Trim();
            }
        }

        EditingComponent.ShowLiveProbe = EditShowLiveProbe;
        EditingComponent.ShowWaveform = EditShowWaveform;

        if (EditShowWaveform)
        {
            WaveformComponent = EditingComponent;
            IsWaveformDrawerOpen = true;
        }

        EditingComponent.NotifyModified(EditingComponent.Reference);
        IsEditDialogVisible = false;
        StatusText = $"Updated {EditingComponent.Reference}";
        CircuitChanged?.Invoke();

        // Trigger simulation to update live probe values
        _ = SimulateAsync();
    }

    [RelayCommand]
    private void DeleteEditingComponent()
    {
        if (EditingComponent != null)
        {
            Circuit.RemoveChild(EditingComponent);
            if (WaveformComponent == EditingComponent)
            {
                WaveformComponent = null;
                IsWaveformDrawerOpen = false;
            }
            StatusText = $"Deleted {EditingComponent.Reference}";
            EditingComponent = null;
        }
        IsEditDialogVisible = false;
    }

    [RelayCommand]
    private void ToggleWaveformDrawer() => IsWaveformDrawerOpen = !IsWaveformDrawerOpen;

    [RelayCommand]
    private void CloseWaveformDrawer() => IsWaveformDrawerOpen = false;

    // ── Helpers ───────────────────────────────────────────────────────────────
    private string GetNextReference(string type)
    {
        string prefix = type switch
        {
            "Resistor"       => "R",
            "Capacitor"      => "C",
            "Inductor"       => "L",
            "VoltageSource"  => "V",
            "CurrentSource"  => "I",
            "Ground"         => "GND",
            "Diode"          => "D",
            "LED"            => "LED",
            "BJT"            => "Q",
            "MOSFET"         => "M",
            "OpAmp"          => "U",
            "LogicGate"      => "U",
            "AND"            => "U",
            "OR"             => "U",
            "NOT"            => "U",
            "NAND"           => "U",
            "NOR"            => "U",
            "XOR"            => "U",
            "XNOR"           => "U",
            "IC"             => "U",
            "Wire"           => "W",
            "Junction"       => "J",
            "Label"          => "LBL",
            "TextAnnotation" => "TXT",
            "Connector"      => "J",
            _                => EESimulator.Core.Components.Registry.ComponentRegistry.TryGetDefinition(type, out var def) ? def.ReferencePrefix : "X"
        };

        int n = 1;
        while (Circuit.FindChildren<ComponentNode>().Any(c => c.Reference == $"{prefix}{n}"))
            n++;
        return $"{prefix}{n}";
    }

    private string? DispatchApiCall(string line)
    {
        // Minimal interpreter for "eesim.method(...)" calls
        if (!line.StartsWith("eesim."))
            return $"[unknown] {line}";

        var call = line["eesim.".Length..];
        var parenIdx = call.IndexOf('(');
        if (parenIdx < 0) return "[error] malformed call";

        var method = call[..parenIdx].Trim();
        var argsStr = call[(parenIdx + 1)..call.LastIndexOf(')')].Trim();
        var args = SplitArgs(argsStr);

        try
        {
            switch (method)
            {
                case "add_component" when args.Length >= 4:
                    PythonApi.AddComponent(Unquote(args[0]), Unquote(args[1]),
                        ParseD(args[2]), ParseD(args[3]),
                        args.Length > 4 ? ParseD(args[4]) : 0);
                    return $"[ok] component {Unquote(args[1])} added";

                case "add_resistor" when args.Length >= 3:
                    PythonApi.AddResistor(Unquote(args[0]), ParseD(args[1]), ParseD(args[2]),
                        args.Length > 3 ? ParseD(args[3]) : 1000.0,
                        args.Length > 4 ? ParseD(args[4]) : 0.0,
                        args.Length > 5 ? ParseD(args[5]) : 0.0,
                        args.Length > 6 ? ParseD(args[6]) : 27.0);
                    return $"[ok] resistor {Unquote(args[0])} added";

                case "add_capacitor" when args.Length >= 3:
                    PythonApi.AddCapacitor(Unquote(args[0]), ParseD(args[1]), ParseD(args[2]),
                        args.Length > 3 ? ParseD(args[3]) : 1e-6,
                        args.Length > 4 ? ParseD(args[4]) : 0.0,
                        args.Length > 5 ? ParseD(args[5]) : 0.0,
                        args.Length > 6 ? ParseD(args[6]) : 0.0,
                        args.Length > 7 ? ParseD(args[7]) : 27.0);
                    return $"[ok] capacitor {Unquote(args[0])} added";

                case "add_inductor" when args.Length >= 3:
                    PythonApi.AddInductor(Unquote(args[0]), ParseD(args[1]), ParseD(args[2]),
                        args.Length > 3 ? ParseD(args[3]) : 1e-3,
                        args.Length > 4 ? ParseD(args[4]) : 0.0,
                        args.Length > 5 ? ParseD(args[5]) : 1.0,
                        args.Length > 6 ? ParseD(args[6]) : 1.0);
                    return $"[ok] inductor {Unquote(args[0])} added";

                case "add_mutual_inductance" when args.Length >= 5:
                    PythonApi.AddMutualInductance(Unquote(args[0]), ParseD(args[1]), ParseD(args[2]),
                        Unquote(args[3]), Unquote(args[4]),
                        args.Length > 5 ? ParseD(args[5]) : 0.99);
                    return $"[ok] mutual inductance {Unquote(args[0])} added";

                case "add_voltage_source" when args.Length >= 3:
                    PythonApi.AddVoltageSource(Unquote(args[0]), ParseD(args[1]), ParseD(args[2]),
                        args.Length > 3 ? ParseD(args[3]) : 5.0,
                        args.Length > 4 ? ParseD(args[4]) : 0.0,
                        args.Length > 5 ? ParseD(args[5]) : 0.0,
                        args.Length > 6 ? Unquote(args[6]) : "DC");
                    return $"[ok] voltage source {Unquote(args[0])} added";

                case "add_current_source" when args.Length >= 3:
                    PythonApi.AddCurrentSource(Unquote(args[0]), ParseD(args[1]), ParseD(args[2]),
                        args.Length > 3 ? ParseD(args[3]) : 0.001,
                        args.Length > 4 ? ParseD(args[4]) : 0.0,
                        args.Length > 5 ? ParseD(args[5]) : 0.0,
                        args.Length > 6 ? Unquote(args[6]) : "DC");
                    return $"[ok] current source {Unquote(args[0])} added";

                case "connect" when args.Length >= 4:
                    PythonApi.Connect(Unquote(args[0]), Unquote(args[1]),
                        Unquote(args[2]), Unquote(args[3]));
                    return "[ok] connected";

                case "set_position" when args.Length >= 3:
                    PythonApi.SetPosition(Unquote(args[0]), ParseD(args[1]), ParseD(args[2]));
                    return "[ok] position set";

                case "set_property" when args.Length >= 3:
                    PythonApi.SetProperty(Unquote(args[0]), Unquote(args[1]), ParseD(args[2]));
                    return "[ok] property set";

                case "set_live_probe" or "set_probe" when args.Length >= 2:
                    PythonApi.SetLiveProbe(Unquote(args[0]), bool.TryParse(Unquote(args[1]), out var lp) && lp);
                    CircuitChanged?.Invoke();
                    return $"[ok] live probe on {Unquote(args[0])} set to {args[1]}";

                case "set_waveform" when args.Length >= 2:
                    PythonApi.SetWaveform(Unquote(args[0]), bool.TryParse(Unquote(args[1]), out var sw) && sw);
                    CircuitChanged?.Invoke();
                    return $"[ok] waveform on {Unquote(args[0])} set to {args[1]}";

                case "rename_pin" when args.Length >= 3:
                    PythonApi.RenamePin(Unquote(args[0]), Unquote(args[1]), Unquote(args[2]));
                    CircuitChanged?.Invoke();
                    return $"[ok] pin {Unquote(args[1])} on {Unquote(args[0])} renamed to {Unquote(args[2])}";

                case "set_wire_resistance" when args.Length >= 2:
                    PythonApi.SetWireResistance(Unquote(args[0]), ParseD(args[1]));
                    CircuitChanged?.Invoke();
                    return $"[ok] wire resistance on {Unquote(args[0])} set to {args[1]} Ω";

                case "register_control_button" when args.Length >= 2:
                    PythonApi.RegisterControlButton(Unquote(args[0]), Unquote(args[1]));
                    return $"[ok] registered control button [{Unquote(args[0])}] -> {Unquote(args[1])}";

                case "set_active_tool" when args.Length >= 1:
                    PythonApi.SetActiveTool(Unquote(args[0]));
                    return $"[ok] active tool set to {Unquote(args[0])}";

                case "simulate":
                    var r = PythonApi.Simulate(args.Length > 0 ? Unquote(args[0]) : "OperatingPoint");
                    return FormatResults(r);

                // UI Hierarchy Operations (RootNode script interface)
                case "ui.open_dialog" or "ui.dialog.open":
                    var targetComp = EditingComponent ?? Circuit.FindChildren<ComponentNode>().FirstOrDefault();
                    if (targetComp != null)
                        OpenEditDialog(targetComp);
                    return targetComp != null ? "[ok] edit dialog opened" : "[error] no component available to edit";

                case "ui.close_dialog" or "ui.dialog.close":
                    CloseEditDialog();
                    return "[ok] edit dialog closed";

                case "ui.toggle_waveform" or "ui.waveform.toggle":
                    ToggleWaveformDrawer();
                    return $"[ok] waveform drawer is now {(IsWaveformDrawerOpen ? "open" : "closed")}";

                case "ui.set_viewport" when args.Length >= 1:
                    PythonApi.SetActiveViewport(Unquote(args[0]));
                    return $"[ok] viewport changed to {Unquote(args[0])}";

                case "ui.get" when args.Length >= 1:
                    var node = UI.Get(Unquote(args[0]));
                    return node != null ? $"[ok] UI node '{node.Name}' ({node.NodeType}), Visible={node.Visible}" : $"[not found] UI node {Unquote(args[0])}";

                case "ui.click" when args.Length >= 1:
                    var btn = UI.Button(Unquote(args[0]));
                    if (btn != null)
                    {
                        btn.Click();
                        return $"[ok] clicked button '{btn.Name}'";
                    }
                    return $"[not found] button {Unquote(args[0])}";

                default:
                    return $"[unknown method] {method}";
            }
        }
        catch (Exception ex)
        {
            return $"[error] {ex.Message}";
        }
    }

    private static string[] SplitArgs(string argsStr)
    {
        // Simple comma-split respecting single quotes
        var result = new System.Collections.Generic.List<string>();
        int depth = 0; bool inQ = false; var cur = new StringBuilder();
        foreach (char c in argsStr)
        {
            if (c == '\'' && depth == 0) { inQ = !inQ; cur.Append(c); }
            else if (!inQ && c == '(') { depth++; cur.Append(c); }
            else if (!inQ && c == ')') { depth--; cur.Append(c); }
            else if (!inQ && c == ',' && depth == 0) { result.Add(cur.ToString().Trim()); cur.Clear(); }
            else cur.Append(c);
        }
        if (cur.Length > 0) result.Add(cur.ToString().Trim());
        return result.ToArray();
    }

    private static string Unquote(string s) =>
        s.Trim().Trim('\'').Trim('"');

    private static double ParseD(string s) =>
        double.TryParse(s.Trim(), System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture, out var v) ? v : 0;

    private static string FormatResults(SimulationResultsData r)
    {
        if (!r.Success)
            return $"Error: {r.Message}";

        var sb = new StringBuilder();
        sb.AppendLine("=== Simulation Results ===");
        foreach (var kv in r.OperatingPointVoltages)
            sb.AppendLine($"  Net {kv.Key}: {kv.Value:F4} V");
        foreach (var kv in r.OperatingPointCurrents)
            sb.AppendLine($"  {kv.Key}: {kv.Value:G4} A");
        if (r.OperatingPointVoltages.Count == 0 && r.OperatingPointCurrents.Count == 0)
            sb.AppendLine("  (No results — check circuit connectivity)");
        return sb.ToString();
    }
}

/// <summary>Record representing an item in the component palette sidebar.</summary>
public record ComponentPaletteItem(
    string Type,
    string Symbol,
    string Unit,
    string Color,
    string Category = "Electrical",
    string Description = "",
    string SvgIcon = "")
{
    public string DisplayText => $"{Symbol}  {Type}";
    public string IconLetter  => Symbol;
    public string TooltipText => $"{Type}  [{Category}]\n{Description}\nUnit: {Unit}";
    public string ImageSource => !string.IsNullOrEmpty(SvgIcon)
        ? (SvgIcon.EndsWith(".svg", StringComparison.OrdinalIgnoreCase) ? SvgIcon[..^4] + ".png" : SvgIcon)
        : "";
    public string SvgSource   => SvgIcon;
    public string AssetName   => !string.IsNullOrEmpty(SvgIcon) && SvgIcon.EndsWith(".svg", StringComparison.OrdinalIgnoreCase)
        ? SvgIcon[..^4]
        : SvgIcon;
}

/// <summary>Editable pin/node model for component edit dialog.</summary>
public class PinEditItem : CommunityToolkit.Mvvm.ComponentModel.ObservableObject
{
    public string Id { get; set; } = string.Empty;

    private string _name = string.Empty;
    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value);
    }

    public PinEditItem() { }

    public PinEditItem(string id, string name)
    {
        Id = id;
        _name = name;
    }
}


