using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Maui.Storage;
using EESimulator.Commands;
using EESimulator.Commands.Commands;
using EESimulator.Core.Models;
using EESimulator.Core.Components.Electrical;
using EESimulator.Core.Components.General;
using EESimulator.Core.Components.Electronics;
using EESimulator.Core.UI;
using EESimulator.Scripting;
using EESimulator.Scripting.API;
using EESimulator.Engine;
using EESimulator.Engine.Simulation;
using EESimulator.Formats;
using EEE.Services;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Devices;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Reflection;
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
    private readonly RemoteEeeApiClient _remoteApi;
    private string? _lastSimulationCircuitSnapshot;
    private readonly HashSet<string> _activeSimulationEventIds = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _simulationEventOwnersByTarget = new(StringComparer.OrdinalIgnoreCase);
    public PythonApi PythonApi { get; }
    private readonly ScriptSynchronizer _sync;
    private readonly Dictionary<string, CustomCircuitDefinition> _customCircuitDefinitions = new(StringComparer.OrdinalIgnoreCase);

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
    public partial string AiSchemaPrompt { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string AiSchemaStatus { get; set; } = "AI drafts use the HCVP server Gemini key; sign-in is not required.";

    [ObservableProperty]
    public partial bool IsAiSchemaRequestInProgress { get; set; }

    [ObservableProperty]
    public partial bool IsSimulating { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsSchematicViewActive))]
    [NotifyPropertyChangedFor(nameof(IsMobileSchematicViewActive))]
    public partial bool IsScriptViewActive { get; set; } = false;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsSchematicViewActive))]
    [NotifyPropertyChangedFor(nameof(IsMobileSchematicViewActive))]
    public partial bool IsAnalyticsViewActive { get; set; }

    public bool IsSchematicViewActive => !IsScriptViewActive && !IsAnalyticsViewActive;
    public bool IsMobileSchematicViewActive => DeviceInfo.Idiom == DeviceIdiom.Phone && IsSchematicViewActive;

    public ObservableCollection<string> AnalyticsModes { get; } = new()
    {
        "Mesh analysis",
        "Nodal analysis",
        "KCL",
        "KVL",
        "Thevenin voltage and current",
        "Superposition"
    };

    [ObservableProperty]
    public partial string SelectedAnalyticsMode { get; set; } = "Nodal analysis";

    [ObservableProperty]
    public partial string AnalyticsOutput { get; set; } = "Choose an analysis and select Run Analysis.";

    [ObservableProperty]
    public partial bool IsAnalyticsRunning { get; set; }

    [ObservableProperty]
    public partial bool IsCreateCircuitComponentDialogVisible { get; set; }

    [ObservableProperty]
    public partial string NewCircuitComponentName { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string NewCircuitComponentSvg { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string CreateCircuitComponentStatus { get; set; } = string.Empty;

    public ObservableCollection<CustomCircuitPortDraft> NewCircuitComponentPorts { get; } = new();

    public ObservableCollection<SimulationEventTrigger> SelectedComponentEventTriggers { get; } = new();

    [ObservableProperty]
    public partial bool IsSimulationEventDialogVisible { get; set; }

    [ObservableProperty]
    public partial string SimulationEventDialogTitle { get; set; } = "Simulation event";

    [ObservableProperty]
    public partial string SimulationEventDialogMessage { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsSimulationEventChatVisible { get; set; }

    [ObservableProperty]
    public partial string SimulationEventChatMessage { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string StatusText { get; set; } = "Ready";

    [ObservableProperty]
    public partial string ActiveComponentInfo { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string ProjectName { get; set; } = "Untitled.eee";

    [ObservableProperty]
    public partial string? CurrentFilePath { get; set; }

    [ObservableProperty]
    public partial bool IsSaveAsModalVisible { get; set; } = false;

    [ObservableProperty]
    public partial string SaveFileName { get; set; } = "Circuit1";

    [ObservableProperty]
    public partial string SaveDestinationFolder { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsFormatEee))]
    [NotifyPropertyChangedFor(nameof(IsFormatKiCad))]
    [NotifyPropertyChangedFor(nameof(IsFormatSpice))]
    [NotifyPropertyChangedFor(nameof(IsFormatEesim))]
    [NotifyPropertyChangedFor(nameof(IsFormatPython))]
    public partial string SelectedSaveFormat { get; set; } = "eee";

    public bool IsFormatEee => SelectedSaveFormat == "eee";
    public bool IsFormatKiCad => SelectedSaveFormat == "kicad";
    public bool IsFormatSpice => SelectedSaveFormat == "spice";
    public bool IsFormatEesim => SelectedSaveFormat == "eesim";
    public bool IsFormatPython => SelectedSaveFormat == "python";


    [ObservableProperty]
    public partial bool CanUndo { get; set; }

    [ObservableProperty]
    public partial bool CanRedo { get; set; }

    [ObservableProperty]
    public partial string ActiveTool { get; set; } = "Select";

    [ObservableProperty]
    public partial bool IsSidebarVisible { get; set; } = true;

    [ObservableProperty]
    public partial bool IsControlSidebarVisible { get; set; } = false;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PlayPauseButtonText))]
    [NotifyPropertyChangedFor(nameof(PlayPauseFabText))]
    public partial bool IsSimulationPlaying { get; set; } = false;


    public string PlayPauseButtonText => IsSimulationPlaying ? "⏸ Pause" : "▶ Run Sim";
    /// <summary>Icon-only text for the compact FAB button on the canvas.</summary>
    public string PlayPauseFabText    => IsSimulationPlaying ? "⏸" : "▶";


    [ObservableProperty]
    public partial float ElectricityAnimPhase { get; set; } = 0f;

    public ObservableCollection<EEE.Services.CustomControlButton> CustomControlButtons =>
        EEE.Services.ControlSidebarService.CustomButtons;

    // Component Edit Dialog state
    [ObservableProperty]
    public partial bool IsEditDialogVisible { get; set; } = false;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsPowerSystemComponent))]
    [NotifyPropertyChangedFor(nameof(IsNotPowerSystemComponent))]
    public partial ComponentNode? EditingComponent { get; set; }

    public bool IsPowerSystemComponent => EditingComponent is PowerSystemComponent;
    public bool IsNotPowerSystemComponent => !IsPowerSystemComponent;
    public ObservableCollection<PowerSystemPropertyItem> PowerSystemProperties { get; } = new();

    /// <summary>The most recently selected component on the canvas (for toolbar actions like Rotate, Copy, Delete).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelectedComponent))]
    [NotifyPropertyChangedFor(nameof(HasSelection))]
    public partial ComponentNode? SelectedComponent { get; set; }

    public bool HasSelectedComponent => SelectedComponent != null;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelection))]
    public partial ConnectionNode? SelectedConnection { get; set; }

    public bool HasSelection => SelectedComponent != null || SelectedConnection != null;

    [ObservableProperty] public partial string EditDescription { get; set; } = string.Empty;
    [ObservableProperty] public partial double EditBreakingCurrent { get; set; } = 1.0;
    [ObservableProperty] public partial bool IsDescriptionSidebarVisible { get; set; } = false;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SimPlayBtnColor))]
    public partial bool HasCircuitOverloadWarning { get; set; } = false;

    public Color SimPlayBtnColor => HasCircuitOverloadWarning ? Color.FromArgb("#FF4444") : Color.FromArgb("#56D364");

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
    [ObservableProperty] public partial bool IsJfetComponent { get; set; } = false;
    [ObservableProperty] public partial bool IsSwitchComponent { get; set; } = false;
    [ObservableProperty] public partial bool IsControlledSourceComponent { get; set; } = false;
    [ObservableProperty] public partial bool IsTransmissionLineComponent { get; set; } = false;
    [ObservableProperty] public partial bool IsVoltageDelayComponent { get; set; } = false;
    [ObservableProperty] public partial bool IsClockComponent { get; set; } = false;
    [ObservableProperty] public partial bool IsPinComponent { get; set; } = false;
    [ObservableProperty] public partial bool IsPowerComponent { get; set; } = false;
    [ObservableProperty] public partial bool IsPullResistorComponent { get; set; } = false;
    [ObservableProperty] public partial bool IsTunnelComponent { get; set; } = false;
    [ObservableProperty] public partial bool IsProbeComponent { get; set; } = false;
    [ObservableProperty] public partial bool IsDigitalTransistorComponent { get; set; } = false;
    [ObservableProperty] public partial bool IsTransmissionGateComponent { get; set; } = false;
    [ObservableProperty] public partial bool IsBitExtenderComponent { get; set; } = false;
    [ObservableProperty] public partial bool IsConstantComponent { get; set; } = false;
    [ObservableProperty] public partial bool IsDoNotConnectComponent { get; set; } = false;
    [ObservableProperty] public partial bool IsTtlPackageComponent { get; set; } = false;
    [ObservableProperty] public partial bool IsLossyTransmissionLineComponent { get; set; } = false;
    [ObservableProperty] public partial bool IsCoaxialCableComponent { get; set; } = false;
    [ObservableProperty] public partial bool IsMicrostripLineComponent { get; set; } = false;

    // Clock
    [ObservableProperty] public partial double EditClockFrequency { get; set; } = 1000.0;
    [ObservableProperty] public partial double EditClockDutyCycle { get; set; } = 0.5;
    [ObservableProperty] public partial double EditClockHighVoltage { get; set; } = 5.0;
    [ObservableProperty] public partial double EditClockLowVoltage { get; set; } = 0.0;
    [ObservableProperty] public partial int EditClockHighDuration { get; set; } = 1;
    [ObservableProperty] public partial int EditClockLowDuration { get; set; } = 1;
    [ObservableProperty] public partial int EditClockPhase { get; set; } = 0;

    // Pin
    [ObservableProperty] public partial string EditPinDirection { get; set; } = "Input";
    [ObservableProperty] public partial int EditPinBitWidth { get; set; } = 1;
    [ObservableProperty] public partial string EditPinBehavior { get; set; } = "Tristate";
    [ObservableProperty] public partial long EditPinInitialValue { get; set; } = 0;
    [ObservableProperty] public partial string EditPinRadix { get; set; } = "Binary";
    [ObservableProperty] public partial double EditPinHighVoltage { get; set; } = 5.0;
    [ObservableProperty] public partial double EditPinLowVoltage { get; set; } = 0.0;

    // Power
    [ObservableProperty] public partial double EditPowerVoltage { get; set; } = 5.0;

    // PullResistor
    [ObservableProperty] public partial string EditPullMode { get; set; } = "PullUp";
    [ObservableProperty] public partial double EditPullResistance { get; set; } = 10000.0;
    [ObservableProperty] public partial double EditPullRailVoltage { get; set; } = 5.0;

    // Tunnel
    [ObservableProperty] public partial string EditTunnelLabel { get; set; } = "NET1";
    [ObservableProperty] public partial int EditTunnelBitWidth { get; set; } = 1;

    // Probe
    [ObservableProperty] public partial string EditProbeRadix { get; set; } = "Binary";
    [ObservableProperty] public partial double EditProbeInputImpedance { get; set; } = 10e6;

    // Digital Transistor
    [ObservableProperty] public partial string EditTransistorType { get; set; } = "NType";
    [ObservableProperty] public partial string EditTransistorGateLocation { get; set; } = "TopRight";
    [ObservableProperty] public partial int EditTransistorBitWidth { get; set; } = 1;
    [ObservableProperty] public partial double EditTransistorRon { get; set; } = 0.1;
    [ObservableProperty] public partial double EditTransistorRoff { get; set; } = 10e6;
    [ObservableProperty] public partial double EditTransistorThreshold { get; set; } = 2.5;

    // Transmission Gate
    [ObservableProperty] public partial string EditTransmissionGateSelectLoc { get; set; } = "TopRight";
    [ObservableProperty] public partial int EditTransmissionGateBitWidth { get; set; } = 1;
    [ObservableProperty] public partial double EditTransmissionGateRon { get; set; } = 0.2;
    [ObservableProperty] public partial double EditTransmissionGateRoff { get; set; } = 10e6;

    // Bit Extender
    [ObservableProperty] public partial int EditBitExtenderInWidth { get; set; } = 8;
    [ObservableProperty] public partial int EditBitExtenderOutWidth { get; set; } = 16;
    [ObservableProperty] public partial string EditBitExtenderType { get; set; } = "SignExtend";

    // Constant
    [ObservableProperty] public partial long EditConstantValue { get; set; } = 1;
    [ObservableProperty] public partial int EditConstantBitWidth { get; set; } = 1;
    [ObservableProperty] public partial double EditConstantVoltageHigh { get; set; } = 5.0;
    [ObservableProperty] public partial double EditConstantVoltageLow { get; set; } = 0.0;

    // TTL package options (Logisim Vcc/Gnd ports and internal structure)
    [ObservableProperty] public partial bool EditTtlExposeSupplyPins { get; set; } = true;
    [ObservableProperty] public partial bool EditTtlShowInternalStructure { get; set; } = false;

    // DoNotConnect
    [ObservableProperty] public partial int EditDncBitWidth { get; set; } = 1;

    // Lossy Transmission Line
    [ObservableProperty] public partial double EditLossyResistance { get; set; } = 0.2;
    [ObservableProperty] public partial double EditLossyInductance { get; set; } = 250e-9;
    [ObservableProperty] public partial double EditLossyConductance { get; set; } = 10e-6;
    [ObservableProperty] public partial double EditLossyCapacitance { get; set; } = 100e-12;
    [ObservableProperty] public partial double EditLossyLength { get; set; } = 1.0;

    // Coaxial Cable
    [ObservableProperty] public partial double EditCoaxZ0 { get; set; } = 50.0;
    [ObservableProperty] public partial double EditCoaxVF { get; set; } = 0.66;
    [ObservableProperty] public partial double EditCoaxLength { get; set; } = 1.0;
    [ObservableProperty] public partial double EditCoaxAttenuation { get; set; } = 0.05;

    // Microstrip Line
    [ObservableProperty] public partial double EditMicrostripWidth { get; set; } = 1.5e-3;
    [ObservableProperty] public partial double EditMicrostripHeight { get; set; } = 0.8e-3;
    [ObservableProperty] public partial double EditMicrostripEr { get; set; } = 4.4;
    [ObservableProperty] public partial double EditMicrostripLength { get; set; } = 0.05;

    [ObservableProperty] public partial string EditJfetChannel { get; set; } = "NChannel";
    [ObservableProperty] public partial double EditResistanceOff { get; set; } = 1e6;
    [ObservableProperty] public partial double EditThreshold { get; set; } = 1.0;
    [ObservableProperty] public partial double EditHysteresis { get; set; } = 0.1;
    [ObservableProperty] public partial string EditControllingSource { get; set; } = "";
    [ObservableProperty] public partial double EditCharacteristicImpedance { get; set; } = 50.0;
    [ObservableProperty] public partial double EditDelayTime { get; set; } = 1e-9;
    [ObservableProperty] public partial double EditLineFrequency { get; set; } = 1e9;
    [ObservableProperty] public partial double EditNormalizedLength { get; set; } = 0.25;

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

    public ObservableCollection<WaveformSignal> WaveformSignals { get; } = new();

    [ObservableProperty]
    public partial bool IsWaveformTracePickerVisible { get; set; }

    [ObservableProperty]
    public partial ComponentNode? WaveformTracePickerComponent { get; set; }

    // Help modal state
    [ObservableProperty]
    public partial bool IsHelpVisible { get; set; } = false;

    // Mobile interaction mode: Pan (default on mobile) or Select
    [ObservableProperty]
    public partial bool IsMobilePanMode { get; set; } = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(LiveMeasurementEyeText))]
    public partial bool IsLiveMeasurementsVisible { get; set; }

    public string LiveMeasurementEyeText => IsLiveMeasurementsVisible ? "👁" : "◉";

    // Component palette items for the sidebar (Electrical, Electronics, General)
    public ObservableCollection<ComponentPaletteItem> ComponentPalette { get; } = new()
    {
        // ── Electrical (Passive & Sources) ───────────────────────────────────
        new ComponentPaletteItem("Resistor",      "R",   "Ω",  "#F78166", "Electrical",  "Linear resistor",       "resistor.svg"),
        new ComponentPaletteItem("Capacitor",     "C",   "F",  "#79C0FF", "Electrical",  "Capacitor",             "capacitor.svg"),
        new ComponentPaletteItem("Inductor",      "L",   "H",  "#D2A8FF", "Electrical",  "Inductor",              "inductor.svg"),
        new ComponentPaletteItem("VoltageSource", "V",   "V",  "#56D364", "Electrical",  "Voltage source",        "voltagesource.svg"),
        new ComponentPaletteItem("CurrentSource", "I",   "A",  "#56D364", "Electrical",  "Current source",        "currentsource.svg"),
        new ComponentPaletteItem("Power",         "VCC", "+V", "#56D364", "Electrical",  "Positive Supply Rail (+VCC)", "power.svg"),
        new ComponentPaletteItem("PullResistor",  "RP",  "PULL","#E3B341", "Electrical",  "Pull-Up / Pull-Down Resistor", "pullresistor.svg"),
        new ComponentPaletteItem("Ground",        "⏚",   "GND","#8B949E", "Electrical",  "Circuit ground",        "ground.svg"),
        new ComponentPaletteItem("Diode",            "D",   "▷|", "#FF7B72", "Electrical",  "Semiconductor diode",      "diode.svg"),
        new ComponentPaletteItem("MutualInductance", "K",   "k",  "#D2A8FF", "Electrical",  "Mutual coupled inductors", "mutualinductance.svg"),
        new ComponentPaletteItem("VCVS",             "E",   "μ",  "#56D364", "Electrical",  "Voltage-Controlled Voltage Source", "vcvs.svg"),
        new ComponentPaletteItem("VCCS",             "G",   "Gm", "#56D364", "Electrical",  "Voltage-Controlled Current Source", "vccs.svg"),
        new ComponentPaletteItem("CCVS",             "H",   "Rm", "#79C0FF", "Electrical",  "Current-Controlled Voltage Source", "ccvs.svg"),
        new ComponentPaletteItem("CCCS",             "F",   "β",  "#79C0FF", "Electrical",  "Current-Controlled Current Source", "cccs.svg"),
        new ComponentPaletteItem("VoltageSwitch",    "S",   "SW", "#E3B341", "Electrical",  "Voltage-Controlled Switch", "voltageswitch.svg"),
        new ComponentPaletteItem("CurrentSwitch",    "W",   "SW", "#E3B341", "Electrical",  "Current-Controlled Switch", "currentswitch.svg"),
        new ComponentPaletteItem("TransmissionLine", "T",   "Ω",  "#79C0FF", "Electrical",  "Lossless Transmission Line", "transmissionline.svg"),
        new ComponentPaletteItem("LossyTransmissionLine", "TL", "RLGC", "#FFA657", "Electrical", "Lossy Transmission Line", "lossy_transmissionline.svg"),
        new ComponentPaletteItem("CoaxialCable",     "CX",  "COAX","#79C0FF", "Electrical",  "Coaxial Cable Line", "coaxial.svg"),
        new ComponentPaletteItem("MicrostripLine",   "MS",  "PCB", "#56D364", "Electrical",  "PCB Microstrip Line", "microstrip.svg"),
        new ComponentPaletteItem("VoltageDelay",     "VD",  "s",  "#56D364", "Electrical",  "Voltage Delay Element",      "voltagedelay.svg"),
        new ComponentPaletteItem("Bulb",             "💡",  "LAMP","#F59E0B", "Electrical",  "Incandescent Light Bulb",    "bulb.svg"),

        // ── Electronics (Semiconductors & Active) ────────────────────────────
        new ComponentPaletteItem("LED",              "💡",  "LED","#E3B341", "Electronics", "Light Emitting Diode",       "led.svg"),
        new ComponentPaletteItem("Clock",            "⏱",  "CLK", "#58A6FF", "Electronics", "Digital Clock Pulse Generator", "clock.svg"),
        new ComponentPaletteItem("Pin",              "⚲",   "PIN", "#58A6FF", "Electronics", "Named I/O Port Terminal", "pin.svg"),
        new ComponentPaletteItem("Constant",         "1",   "CST", "#56D364", "Electronics", "Logic Constant Source (1/0)", "constant.svg"),
        new ComponentPaletteItem("LogicInput",       "🔲",  "IN",  "#56D364", "Electronics", "Toggle Logic Input (0/1)",   "logicinput.svg"),
        new ComponentPaletteItem("LogicOutput",      "🎯",  "OUT", "#79C0FF", "Electronics", "Digital Logic Probe (0/1)",  "logicoutput.svg"),
        new ComponentPaletteItem("Probe",            "👁",  "PRB", "#79C0FF", "Electronics", "Voltage/Current Probe Point", "probe.svg"),
        new ComponentPaletteItem("Transistor",       "SW",  "SWT", "#FFA657", "Electronics", "Digital Switch Transistor", "transistor_digital.svg"),
        new ComponentPaletteItem("TransmissionGate", "TG",  "TG",  "#D2A8FF", "Electronics", "CMOS Transmission Gate", "transmissiongate.svg"),
        new ComponentPaletteItem("BitExtender",      "⇄",   "EXT", "#79C0FF", "Electronics", "Bus Bit Extender", "bitextender.svg"),
        new ComponentPaletteItem("NPN",              "Q",   "NPN", "#FFA657", "Electronics", "BJT NPN Transistor",         "bjt_npn.svg"),
        new ComponentPaletteItem("PNP",              "Q",   "PNP", "#FFA657", "Electronics", "BJT PNP Transistor",    "bjt_pnp.svg"),
        new ComponentPaletteItem("JFET",             "J",   "FET", "#FFA657", "Electronics", "Junction Field-Effect",  "jfet.svg"),
        new ComponentPaletteItem("NMOS",             "M",   "NMOS","#D2A8FF", "Electronics", "N-Channel MOSFET",     "nmos.svg"),
        new ComponentPaletteItem("PMOS",             "M",   "PMOS","#D2A8FF", "Electronics", "P-Channel MOSFET",     "pmos.svg"),
        new ComponentPaletteItem("OpAmp",            "▷",   "AMP", "#79C0FF", "Electronics", "Operational Amplifier", "opamp.svg"),
        new ComponentPaletteItem("AND",              "&",   "AND", "#58A6FF", "Electronics", "2-Input AND Gate",      "and.svg"),
        new ComponentPaletteItem("OR",               "≥1",  "OR",  "#58A6FF", "Electronics", "2-Input OR Gate",       "or.svg"),
        new ComponentPaletteItem("NOT",              "¬",   "NOT", "#58A6FF", "Electronics", "Inverter / NOT Gate",   "not.svg"),
        new ComponentPaletteItem("NAND",             "⊼",   "NND", "#58A6FF", "Electronics", "2-Input NAND Gate",     "nand.svg"),
        new ComponentPaletteItem("NOR",              "⊽",   "NOR", "#58A6FF", "Electronics", "2-Input NOR Gate",      "nor.svg"),
        new ComponentPaletteItem("XOR",              "=1",  "XOR", "#58A6FF", "Electronics", "2-Input XOR Gate",      "xor.svg"),
        new ComponentPaletteItem("XNOR",             "⊙",   "XNR", "#58A6FF", "Electronics", "2-Input XNOR Gate",     "xnor.svg"),
        new ComponentPaletteItem("LogicGate",        "⊳",   "GATE","#58A6FF", "Electronics", "Digital Logic Gate",      "logicgate.svg"),
        new ComponentPaletteItem("IC",               "⊞",   "IC",  "#8B949E", "Electronics", "Integrated Circuit",    "ic.svg"),
        new ComponentPaletteItem("Buffer",           "▷",   "BUF", "#58A6FF", "Gates", "Digital Buffer", "buffer.svg"),
        new ComponentPaletteItem("ControlledBuffer",  "▷○",  "TRI", "#58A6FF", "Gates", "Tri-state Buffer", "controlledbuffer.svg"),
        new ComponentPaletteItem("EvenParity",       "⊙",   "EPAR", "#58A6FF", "Gates", "Even Parity Gate", "evenparity.svg"),
        new ComponentPaletteItem("OddParity",        "⊕",   "OPAR", "#58A6FF", "Gates", "Odd Parity Gate", "oddparity.svg"),
        new ComponentPaletteItem("PLA",              "▦",   "PLA", "#58A6FF", "Gates", "Programmable Logic Array", "pla.svg"),
        new ComponentPaletteItem("Multiplexer",      "MUX", "MUX", "#79C0FF", "Plexers", "N-to-1 Data Selector", "mux.svg"),
        new ComponentPaletteItem("Demultiplexer",    "DMX", "DMX", "#79C0FF", "Plexers", "1-to-N Data Distributor", "demux.svg"),
        new ComponentPaletteItem("Decoder",          "DEC", "DEC", "#79C0FF", "Plexers", "Binary Decoder", "decoder.svg"),
        new ComponentPaletteItem("PriorityEncoder",  "ENC", "ENC", "#79C0FF", "Plexers", "Priority Encoder", "priorityencoder.svg"),
        new ComponentPaletteItem("BitSelector",      "SEL", "BSEL", "#79C0FF", "Plexers", "Bus Bit Selector", "bitselector.svg"),
        new ComponentPaletteItem("DFlipFlop",        "DFF", "DFF", "#D2A8FF", "Memory", "D Flip-Flop", "dff.svg"),
        new ComponentPaletteItem("JKFlipFlop",       "JK",  "JKFF", "#D2A8FF", "Memory", "JK Flip-Flop", "jkff.svg"),
        new ComponentPaletteItem("SRFlipFlop",       "SR",  "SRFF", "#D2A8FF", "Memory", "SR Flip-Flop", "srff.svg"),
        new ComponentPaletteItem("TFlipFlop",        "TFF", "TFF", "#D2A8FF", "Memory", "T Flip-Flop", "tff.svg"),
        new ComponentPaletteItem("Register",         "REG", "REG", "#D2A8FF", "Memory", "N-bit Register", "register.svg"),
        new ComponentPaletteItem("Counter",          "CTR", "CTR", "#D2A8FF", "Memory", "Up/Down Counter", "counter.svg"),
        new ComponentPaletteItem("ShiftRegister",    "SHR", "SHR", "#D2A8FF", "Memory", "Shift Register", "shiftregister.svg"),
        new ComponentPaletteItem("RAM",              "RAM", "RAM", "#D2A8FF", "Memory", "Read/Write Memory", "ram.svg"),
        new ComponentPaletteItem("ROM",              "ROM", "ROM", "#D2A8FF", "Memory", "Read-Only Memory", "rom.svg"),
        new ComponentPaletteItem("Random",           "RND", "RND", "#D2A8FF", "Memory", "Pseudo-Random Generator", "random.svg"),

        new ComponentPaletteItem("Adder", "＋", "ADD", "#FFB86C", "Arithmetic", "N-bit binary adder", "adder.svg"),
        new ComponentPaletteItem("Subtractor", "−", "SUB", "#FFB86C", "Arithmetic", "N-bit binary subtractor", "subtractor.svg"),
        new ComponentPaletteItem("Multiplier", "×", "MUL", "#FFB86C", "Arithmetic", "N-bit binary multiplier", "multiplier.svg"),
        new ComponentPaletteItem("Divider", "÷", "DIV", "#FFB86C", "Arithmetic", "N-bit binary divider", "divider.svg"),
        new ComponentPaletteItem("Comparator", "≷", "CMP", "#FFB86C", "Arithmetic", "Magnitude comparator", "comparator.svg"),
        new ComponentPaletteItem("Negator", "−x", "NEG", "#FFB86C", "Arithmetic", "Two's-complement negator", "negator.svg"),
        new ComponentPaletteItem("Shifter", "⇆", "SHF", "#FFB86C", "Arithmetic", "Barrel shifter", "shifter.svg"),
        new ComponentPaletteItem("BitAdder", "Σ1", "POP", "#FFB86C", "Arithmetic", "Population count", "bitadder.svg"),
        new ComponentPaletteItem("BitFinder", "⌕", "FND", "#FFB86C", "Arithmetic", "Find first/last set bit", "bitfinder.svg"),
        new ComponentPaletteItem("SquareRoot", "√", "SQR", "#FFB86C", "Arithmetic", "Integer square root", "sqrt.svg"),
        new ComponentPaletteItem("AbsoluteValue", "|x|", "ABS", "#FFB86C", "Arithmetic", "Absolute value", "absolute.svg"),
        new ComponentPaletteItem("MinMax", "↕", "MM", "#FFB86C", "Arithmetic", "Minimum/maximum selector", "minmax.svg"),
        new ComponentPaletteItem("Exponentiator", "xʸ", "POW", "#FFB86C", "Arithmetic", "Integer exponentiation", "exponentiator.svg"),

        new ComponentPaletteItem("Button", "●", "BTN", "#56D364", "I/O", "Momentary push-button", "button.svg"),
        new ComponentPaletteItem("DIPSwitch", "▤", "DIP", "#56D364", "I/O", "DIP switch bank", "dipswitch.svg"),
        new ComponentPaletteItem("SevenSegment", "8", "7SEG", "#56D364", "I/O", "Seven-segment display", "sevensegment.svg"),
        new ComponentPaletteItem("HexDigit", "A", "HEX", "#56D364", "I/O", "Hex digit display", "hexdigit.svg"),
        new ComponentPaletteItem("LedBar", "▥", "LBA", "#56D364", "I/O", "LED bar array", "ledbar.svg"),
        new ComponentPaletteItem("RGBLED", "●", "RGB", "#56D364", "I/O", "RGB LED indicator", "rgbled.svg"),
        new ComponentPaletteItem("DotMatrix", "▦", "DM", "#56D364", "I/O", "Dot matrix display", "dotmatrix.svg"),
        new ComponentPaletteItem("Joystick", "✥", "JOY", "#56D364", "I/O", "Two-axis joystick input", "joystick.svg"),
        new ComponentPaletteItem("Keyboard", "⌨", "KEY", "#56D364", "I/O", "ASCII keyboard buffer", "keyboard.svg"),
        new ComponentPaletteItem("TTY", "▣", "TTY", "#56D364", "I/O", "Serial text terminal", "tty.svg"),
        new ComponentPaletteItem("Video", "▧", "VID", "#56D364", "I/O", "Pixel framebuffer", "video.svg"),
        new ComponentPaletteItem("RealTimeClock", "◷", "RTC", "#56D364", "I/O", "Wall-clock time source", "realtimeclock.svg"),

        // ── General (Structural & Schematic) ─────────────────────────────────
        new ComponentPaletteItem("Wire",          "—",   "W",   "#58A6FF", "General", "Wire segment",         "wire.svg"),
        new ComponentPaletteItem("Junction",      "•",   "J",   "#58A6FF", "General", "Wire junction",        "junction.svg"),
        new ComponentPaletteItem("Tunnel",        "TUN", "NET", "#56D364", "General", "Named Net Tunnel",     "tunnel.svg"),
        new ComponentPaletteItem("Label",         "🏷",  "LBL", "#56D364", "General", "Net label",            "label.svg"),
        new ComponentPaletteItem("DoNotConnect",  "✕",   "DNC", "#F78166", "General", "Do Not Connect Marker","dnc.svg"),
        new ComponentPaletteItem("TextAnnotation","🗎",  "TXT", "#8B949E", "General", "Text note",            "textannotation.svg"),
        new ComponentPaletteItem("Connector",     "⑉",   "HDR", "#E3B341", "General", "Header connector",     "connector.svg")
    };

    /// <summary>Palette grouped by category for the sidebar 2-per-row grid layout.</summary>
    public ObservableCollection<ComponentPaletteGroup> GroupedPalette { get; } = new()
    {
        new ComponentPaletteGroup("⚡ Passives", new[]
        {
            new ComponentPaletteItem("Resistor",      "R",  "Ω",    "#F78166", "Passives",  "Linear resistor",          "resistor.svg"),
            new ComponentPaletteItem("Capacitor",     "C",  "F",    "#79C0FF", "Passives",  "Capacitor",                "capacitor.svg"),
            new ComponentPaletteItem("Inductor",      "L",  "H",    "#D2A8FF", "Passives",  "Inductor",                 "inductor.svg"),
            new ComponentPaletteItem("MutualInductance","K","k",    "#D2A8FF", "Passives",  "Mutual coupled inductors", "mutualinductance.svg"),
            new ComponentPaletteItem("PullResistor",  "RP", "PULL", "#E3B341", "Passives",  "Pull-Up / Pull-Down Resistor", "pullresistor.svg"),
        }),
        new ComponentPaletteGroup("🔋 Sources & Power", new[]
        {
            new ComponentPaletteItem("VoltageSource", "V",   "V",   "#56D364", "Sources",   "Voltage source",           "voltagesource.svg"),
            new ComponentPaletteItem("CurrentSource", "I",   "A",   "#56D364", "Sources",   "Current source",           "currentsource.svg"),
            new ComponentPaletteItem("Power",         "VCC", "+V",  "#56D364", "Sources",   "Positive Supply Rail (+VCC)", "power.svg"),
            new ComponentPaletteItem("Constant",      "1",   "CST", "#56D364", "Sources",   "Logic Constant (1/0)",     "constant.svg"),
            new ComponentPaletteItem("Clock",         "⏱",   "CLK", "#58A6FF", "Sources",   "Digital Clock Generator",  "clock.svg"),
            new ComponentPaletteItem("VoltageDelay",  "VD",  "s",   "#56D364", "Sources",   "Voltage Delay Element",    "voltagedelay.svg"),
            new ComponentPaletteItem("Ground",        "⏚",   "GND", "#8B949E", "Sources",   "Circuit ground",           "ground.svg"),
        }),
        new ComponentPaletteGroup("🔀 Dependent Sources", new[]
        {
            new ComponentPaletteItem("VCVS",   "E",  "μ",  "#56D364", "Dep.Sources", "Voltage-Controlled Voltage Source", "vcvs.svg"),
            new ComponentPaletteItem("VCCS",   "G",  "Gm", "#56D364", "Dep.Sources", "Voltage-Controlled Current Source", "vccs.svg"),
            new ComponentPaletteItem("CCVS",   "H",  "Rm", "#79C0FF", "Dep.Sources", "Current-Controlled Voltage Source", "ccvs.svg"),
            new ComponentPaletteItem("CCCS",   "F",  "β",  "#79C0FF", "Dep.Sources", "Current-Controlled Current Source", "cccs.svg"),
        }),
        new ComponentPaletteGroup("🔘 Diodes", new[]
        {
            new ComponentPaletteItem("Diode",  "D",  "▷|","#FF7B72", "Diodes",  "Semiconductor diode",   "diode.svg"),
            new ComponentPaletteItem("LED",    "💡", "LED","#E3B341", "Diodes",  "Light Emitting Diode",  "led.svg"),
        }),
        new ComponentPaletteGroup("💡 Output & Logic I/O", new[]
        {
            new ComponentPaletteItem("Bulb",        "💡", "LAMP", "#F59E0B", "Output",  "Incandescent Light Bulb",   "bulb.svg"),
            new ComponentPaletteItem("LED",         "💡", "LED",  "#E3B341", "Output",  "Light Emitting Diode",      "led.svg"),
            new ComponentPaletteItem("LogicInput",  "🔲", "IN",   "#56D364", "Digital", "Toggle Logic Input (0/1)",  "logicinput.svg"),
            new ComponentPaletteItem("LogicOutput", "🎯", "OUT",  "#79C0FF", "Digital", "Digital Logic Probe (0/1)", "logicoutput.svg"),
            new ComponentPaletteItem("Pin",         "⚲",  "PIN",  "#58A6FF", "Digital", "Named I/O Port Terminal",   "pin.svg"),
            new ComponentPaletteItem("Probe",       "👁",  "PRB",  "#79C0FF", "Digital", "Voltage/Current Probe Point", "probe.svg"),
            new ComponentPaletteItem("BitExtender", "⇄",  "EXT",  "#79C0FF", "Digital", "Bus Bit Extender",          "bitextender.svg"),
        }),
        new ComponentPaletteGroup("⚙ Switches", new[]
        {
            new ComponentPaletteItem("VoltageSwitch",   "S",  "SW", "#E3B341", "Switches", "Voltage-Controlled Switch", "voltageswitch.svg"),
            new ComponentPaletteItem("CurrentSwitch",   "W",  "SW", "#E3B341", "Switches", "Current-Controlled Switch", "currentswitch.svg"),
            new ComponentPaletteItem("Transistor",      "SW", "SWT", "#FFA657", "Switches", "Digital Switch Transistor", "transistor_digital.svg"),
            new ComponentPaletteItem("TransmissionGate", "TG", "TG", "#D2A8FF", "Switches", "CMOS Transmission Gate",    "transmissiongate.svg"),
        }),
        new ComponentPaletteGroup("📡 Transmission Lines", new[]
        {
            new ComponentPaletteItem("SinglePhaseTransmissionLine", "T1", "RLGC", "#79C0FF", "Transmission Lines", "Single-phase line: short, medium, or long-line model", "transmissionline.svg"),
            new ComponentPaletteItem("TwoPhaseTransmissionLine", "T2", "RLGC", "#79C0FF", "Transmission Lines", "True two-phase or split-phase three-wire line", "transmissionline.svg"),
            new ComponentPaletteItem("ThreePhaseTransmissionLine", "T3", "RLGC", "#79C0FF", "Transmission Lines", "Three-phase line with explicit phase/neutral nodes", "transmissionline.svg"),
            new ComponentPaletteItem("TransmissionLine",      "T",  "Ω",   "#79C0FF", "Transmission", "Lossless Transmission Line", "transmissionline.svg"),
            new ComponentPaletteItem("LossyTransmissionLine", "TL", "RLGC","#FFA657", "Transmission", "Lossy Transmission Line",    "lossy_transmissionline.svg"),
            new ComponentPaletteItem("CoaxialCable",          "CX", "COAX","#79C0FF", "Transmission", "Coaxial Cable Line",         "coaxial.svg"),
            new ComponentPaletteItem("MicrostripLine",        "MS", "PCB", "#56D364", "Transmission", "PCB Microstrip Line",        "microstrip.svg"),
        }),
        new ComponentPaletteGroup("⚡ Electrical Machines", new[]
        {
            new ComponentPaletteItem("ACTransformer", "TR", "VA", "#D2A8FF", "Electrical Machines", "Single- or three-phase transformer equivalent circuit", "transformer.svg"),
            new ComponentPaletteItem("SynchronousGenerator", "GEN", "VA", "#56D364", "Electrical Machines", "Steady-state synchronous generator", "generator.svg"),
            new ComponentPaletteItem("InductionMotor", "MTR", "W", "#FFA657", "Electrical Machines", "Steady-state induction motor load", "motor.svg"),
        }),
        new ComponentPaletteGroup("🏠 Electrical Loads", new[]
        {
            new ComponentPaletteItem("ACBulb", "ACB", "W", "#F59E0B", "Electrical Loads", "Household AC bulb load", "bulb.svg"),
            new ComponentPaletteItem("ACFan", "FAN", "W", "#79C0FF", "Electrical Loads", "Household AC fan motor load", "fan.svg"),
        }),
        new ComponentPaletteGroup("🌞 Sources & Energy", new[]
        {
            new ComponentPaletteItem("SolarPanel", "PV", "Wp", "#E3B341", "Sources & Energy", "Single-diode photovoltaic panel", "solarpanel.svg"),
        }),
        new ComponentPaletteGroup("🔋 Energy Storage", new[]
        {
            new ComponentPaletteItem("Battery", "BAT", "Ah", "#56D364", "Energy Storage", "SOC-dependent battery with internal resistance", "battery.svg"),
        }),
        new ComponentPaletteGroup("🔌 Semiconductors", new[]
        {
            new ComponentPaletteItem("NPN",   "Q",  "NPN", "#FFA657", "Semi", "BJT NPN Transistor",    "bjt_npn.svg"),
            new ComponentPaletteItem("PNP",   "Q",  "PNP", "#FFA657", "Semi", "BJT PNP Transistor",    "bjt_pnp.svg"),
            new ComponentPaletteItem("JFET",  "J",   "FET", "#FFA657", "Semi", "Junction Field-Effect",  "jfet.svg"),
            new ComponentPaletteItem("NMOS",  "M",   "NMOS","#D2A8FF", "Semi", "N-Channel MOSFET",     "nmos.svg"),
            new ComponentPaletteItem("PMOS",  "M",   "PMOS","#D2A8FF", "Semi", "P-Channel MOSFET",     "pmos.svg"),
            new ComponentPaletteItem("OpAmp", "▷",   "AMP", "#79C0FF", "Semi", "Operational Amplifier","opamp.svg"),
        }),
        new ComponentPaletteGroup("🧠 Logic Gates", new[]
        {
            new ComponentPaletteItem("AND",  "&",  "AND", "#58A6FF", "Logic", "2-Input AND Gate",  "and.svg"),
            new ComponentPaletteItem("OR",   "≥1", "OR",  "#58A6FF", "Logic", "2-Input OR Gate",   "or.svg"),
            new ComponentPaletteItem("NOT",  "¬",  "NOT", "#58A6FF", "Logic", "Inverter / NOT",    "not.svg"),
            new ComponentPaletteItem("NAND", "⊼",  "NND", "#58A6FF", "Logic", "2-Input NAND Gate", "nand.svg"),
            new ComponentPaletteItem("NOR",  "⊽",  "NOR", "#58A6FF", "Logic", "2-Input NOR Gate",  "nor.svg"),
            new ComponentPaletteItem("XOR",  "=1", "XOR", "#58A6FF", "Logic", "2-Input XOR Gate",  "xor.svg"),
            new ComponentPaletteItem("XNOR", "⊙",  "XNR", "#58A6FF", "Logic", "2-Input XNOR Gate", "xnor.svg"),
            new ComponentPaletteItem("IC",   "⊞",  "IC",  "#8B949E", "Logic", "Integrated Circuit","ic.svg"),
        }),
        new ComponentPaletteGroup("▦ Additional Logic Gates", new[]
        {
            new ComponentPaletteItem("Buffer",          "▷",  "BUF",  "#58A6FF", "Gates", "Unity-gain digital buffer", "buffer.svg"),
            new ComponentPaletteItem("ControlledBuffer", "▷○", "TRI",  "#58A6FF", "Gates", "Tri-state buffer with enable", "controlledbuffer.svg"),
            new ComponentPaletteItem("EvenParity",      "⊙",  "EPAR", "#58A6FF", "Gates", "Even parity check", "evenparity.svg"),
            new ComponentPaletteItem("OddParity",       "⊕",  "OPAR", "#58A6FF", "Gates", "Odd parity check", "oddparity.svg"),
            new ComponentPaletteItem("PLA",             "▦",  "PLA",  "#58A6FF", "Gates", "Programmable logic array", "pla.svg"),
        }),
        new ComponentPaletteGroup("🔀 Plexers", new[]
        {
            new ComponentPaletteItem("Multiplexer",     "MUX", "MUX",  "#79C0FF", "Plexers", "N-to-1 data selector", "mux.svg"),
            new ComponentPaletteItem("Demultiplexer",   "DMX", "DMX",  "#79C0FF", "Plexers", "1-to-N data distributor", "demux.svg"),
            new ComponentPaletteItem("Decoder",         "DEC", "DEC",  "#79C0FF", "Plexers", "Binary decoder", "decoder.svg"),
            new ComponentPaletteItem("PriorityEncoder", "ENC", "ENC",  "#79C0FF", "Plexers", "Priority encoder", "priorityencoder.svg"),
            new ComponentPaletteItem("BitSelector",     "SEL", "BSEL", "#79C0FF", "Plexers", "Select a bit from a bus", "bitselector.svg"),
        }),
        new ComponentPaletteGroup("🧠 Memory & State", new[]
        {
            new ComponentPaletteItem("DFlipFlop",     "DFF", "DFF", "#D2A8FF", "Memory", "D flip-flop", "dff.svg"),
            new ComponentPaletteItem("JKFlipFlop",    "JK",  "JKFF", "#D2A8FF", "Memory", "JK flip-flop", "jkff.svg"),
            new ComponentPaletteItem("SRFlipFlop",    "SR",  "SRFF", "#D2A8FF", "Memory", "SR flip-flop", "srff.svg"),
            new ComponentPaletteItem("TFlipFlop",     "TFF", "TFF", "#D2A8FF", "Memory", "T flip-flop", "tff.svg"),
            new ComponentPaletteItem("Register",      "REG", "REG", "#D2A8FF", "Memory", "Parallel-load register", "register.svg"),
            new ComponentPaletteItem("Counter",       "CTR", "CTR", "#D2A8FF", "Memory", "Up/down counter", "counter.svg"),
            new ComponentPaletteItem("ShiftRegister", "SHR", "SHR", "#D2A8FF", "Memory", "Shift register", "shiftregister.svg"),
            new ComponentPaletteItem("RAM",           "RAM", "RAM", "#D2A8FF", "Memory", "Read/write memory", "ram.svg"),
            new ComponentPaletteItem("ROM",           "ROM", "ROM", "#D2A8FF", "Memory", "Read-only memory", "rom.svg"),
            new ComponentPaletteItem("Random",        "RND", "RND", "#D2A8FF", "Memory", "Pseudo-random generator", "random.svg"),
        }),
        new ComponentPaletteGroup("📦 74-Series TTL", TtlPackageComponent.SupportedPackages.Keys
            .OrderBy(part => part, StringComparer.Ordinal)
            .Select(part => new ComponentPaletteItem(part, part, "TTL", "#58A6FF", "TTL", $"74-series {part}, {TtlPackageComponent.GetPackagePinCount(part)}-pin DIP package",
            .ToArray()),
        new ComponentPaletteGroup("∑ Arithmetic", new[]
        {
            new ComponentPaletteItem("Adder", "＋", "ADD", "#FFB86C", "Arithmetic", "N-bit binary adder", "adder.svg"),
            new ComponentPaletteItem("Subtractor", "−", "SUB", "#FFB86C", "Arithmetic", "N-bit binary subtractor", "subtractor.svg"),
            new ComponentPaletteItem("Multiplier", "×", "MUL", "#FFB86C", "Arithmetic", "N-bit binary multiplier", "multiplier.svg"),
            new ComponentPaletteItem("Divider", "÷", "DIV", "#FFB86C", "Arithmetic", "N-bit binary divider", "divider.svg"),
            new ComponentPaletteItem("Comparator", "≷", "CMP", "#FFB86C", "Arithmetic", "Magnitude comparator", "comparator.svg"),
            new ComponentPaletteItem("Negator", "−x", "NEG", "#FFB86C", "Arithmetic", "Two's-complement negator", "negator.svg"),
            new ComponentPaletteItem("Shifter", "⇆", "SHF", "#FFB86C", "Arithmetic", "Barrel shifter", "shifter.svg"),
            new ComponentPaletteItem("BitAdder", "Σ1", "POP", "#FFB86C", "Arithmetic", "Population count", "bitadder.svg"),
            new ComponentPaletteItem("BitFinder", "⌕", "FND", "#FFB86C", "Arithmetic", "Find first/last set bit", "bitfinder.svg"),
            new ComponentPaletteItem("SquareRoot", "√", "SQR", "#FFB86C", "Arithmetic", "Integer square root", "sqrt.svg"),
            new ComponentPaletteItem("AbsoluteValue", "|x|", "ABS", "#FFB86C", "Arithmetic", "Absolute value", "absolute.svg"),
            new ComponentPaletteItem("MinMax", "↕", "MM", "#FFB86C", "Arithmetic", "Minimum/maximum selector", "minmax.svg"),
            new ComponentPaletteItem("Exponentiator", "xʸ", "POW", "#FFB86C", "Arithmetic", "Integer exponentiation", "exponentiator.svg"),
        }),
        new ComponentPaletteGroup("🖥 I/O & Displays", new[]
        {
            new ComponentPaletteItem("Button", "●", "BTN", "#56D364", "I/O", "Momentary push-button", "button.svg"),
            new ComponentPaletteItem("DIPSwitch", "▤", "DIP", "#56D364", "I/O", "DIP switch bank", "dipswitch.svg"),
            new ComponentPaletteItem("SevenSegment", "8", "7SEG", "#56D364", "I/O", "Seven-segment display", "sevensegment.svg"),
            new ComponentPaletteItem("HexDigit", "A", "HEX", "#56D364", "I/O", "Hex digit display", "hexdigit.svg"),
            new ComponentPaletteItem("LedBar", "▥", "LBA", "#56D364", "I/O", "LED bar array", "ledbar.svg"),
            new ComponentPaletteItem("RGBLED", "●", "RGB", "#56D364", "I/O", "RGB LED indicator", "rgbled.svg"),
            new ComponentPaletteItem("DotMatrix", "▦", "DM", "#56D364", "I/O", "Dot matrix display", "dotmatrix.svg"),
            new ComponentPaletteItem("Joystick", "✥", "JOY", "#56D364", "I/O", "Two-axis joystick input", "joystick.svg"),
            new ComponentPaletteItem("Keyboard", "⌨", "KEY", "#56D364", "I/O", "ASCII keyboard buffer", "keyboard.svg"),
            new ComponentPaletteItem("TTY", "▣", "TTY", "#56D364", "I/O", "Serial text terminal", "tty.svg"),
            new ComponentPaletteItem("Video", "▧", "VID", "#56D364", "I/O", "Pixel framebuffer", "video.svg"),
            new ComponentPaletteItem("RealTimeClock", "◷", "RTC", "#56D364", "I/O", "Wall-clock time source", "realtimeclock.svg"),
        }),
        new ComponentPaletteGroup("🗂 Schematic", new[]
        {
            new ComponentPaletteItem("Wire",           "—",   "W",   "#58A6FF", "Schematic", "Wire segment",          "wire.svg"),
            new ComponentPaletteItem("Junction",       "•",   "J",   "#58A6FF", "Schematic", "Wire junction",         "junction.svg"),
            new ComponentPaletteItem("Tunnel",         "TUN", "NET", "#56D364", "Schematic", "Named Net Tunnel",     "tunnel.svg"),
            new ComponentPaletteItem("Label",          "🏷",  "LBL", "#56D364", "Schematic", "Net label",             "label.svg"),
            new ComponentPaletteItem("DoNotConnect",   "✕",   "DNC", "#F78166", "Schematic", "Do Not Connect Marker",  "dnc.svg"),
            new ComponentPaletteItem("TextAnnotation", "🗎",  "TXT", "#8B949E", "Schematic", "Text note",             "textannotation.svg"),
            new ComponentPaletteItem("Connector",      "⑉",   "HDR", "#E3B341", "Schematic", "Header connector",      "connector.svg"),
        }),
    };

    /// <summary>Raised when the circuit changes so the canvas can Invalidate immediately.</summary>
    public event Action? CircuitChanged;
    public event Action? ViewportRecenterRequested;

    // ── Constructor ──────────────────────────────────────────────────────────
    public EEWorkspaceViewModel(RemoteEeeApiClient remoteApi)
    {
        _remoteApi = remoteApi ?? throw new ArgumentNullException(nameof(remoteApi));
        _simManager = new SimulationManager();
        PythonApi   = new PythonApi(CommandManager, Circuit, _simManager, UI);
        _sync       = new ScriptSynchronizer(CommandManager, Circuit);

        // Register default UI nodes under RootNode hierarchy
        UI.Workspace.Title = "Schematic Workspace";

        // 1. Sidebars (Component Sidebar & Control Sidebar)
        var componentSidebar = new SidebarNode("ComponentSidebar", "Left") { Title = "Component Palette" };
        var paletteSidebar = new SidebarNode("ComponentPalette", "Left") { Title = "Component Palette" };
        var controlSidebar = new SidebarNode("ControlSidebar", "Right") { Title = "Control Sidebar" };
        var editDialog = new DialogNode("ComponentEditDialog", "Edit Component");

        // 2. Fixed Simulation Play / Pause / Stop Buttons
        var playBtn = new ButtonNode("PlaySim", "Play Simulation", () => { if (!IsSimulationPlaying) ToggleSimulationPlayback(); });
        var pauseBtn = new ButtonNode("PauseSim", "Pause Simulation", () => { if (IsSimulationPlaying) ToggleSimulationPlayback(); });
        var togglePlayPauseBtn = new ButtonNode("TogglePlayPause", "Play/Pause Simulation", () => ToggleSimulationPlayback());
        var stopBtn = new ButtonNode("StopSim", "Stop Simulation", () => { if (IsSimulationPlaying) ToggleSimulationPlayback(); });
        var runButton = new ButtonNode("RunSim", "Run Simulation", () => _ = SimulateAsync());
        var waveButton = new ButtonNode("ToggleWaveform", "Toggle Waveform", () => ToggleWaveformDrawer());

        // 3. Top Page NavBar section
        var navBar = new UINode { Name = "TopNavBar", Title = "Top Navigation Bar" };
        var navTitle = new InputNode("ProjectTitle", ProjectName);
        var navPlayPause = new ButtonNode("NavPlayPause", "Toggle Play/Pause", () => ToggleSimulationPlayback());
        var navUndo = new ButtonNode("NavUndo", "Undo", () => Undo());
        var navRedo = new ButtonNode("NavRedo", "Redo", () => Redo());
        var navRotate = new ButtonNode("NavRotate", "Rotate Selected Component", () => RotateSelectedComponent());
        var navViewToggle = new ButtonNode("NavViewToggle", "Toggle Script/Schematic View", () => ToggleViewport());
        var navWaveform = new ButtonNode("NavWaveform", "Toggle Waveform", () => ToggleWaveformDrawer());
        var navSimSettings = new ButtonNode("NavSimSettings", "Simulation Settings", () => OpenSimSettings());
        var navSettings = new ButtonNode("NavSettings", "Settings", () => _ = OpenSettings());
        var navHelp = new ButtonNode("NavHelp", "Help", () => ToggleHelp());

        navBar.AddChild(navTitle);
        navBar.AddChild(navPlayPause);
        navBar.AddChild(navUndo);
        navBar.AddChild(navRedo);
        navBar.AddChild(navRotate);
        navBar.AddChild(navViewToggle);
        navBar.AddChild(navWaveform);
        navBar.AddChild(navSimSettings);
        navBar.AddChild(navSettings);
        navBar.AddChild(navHelp);

        UI.Workspace.AddChild(componentSidebar);
        UI.Workspace.AddChild(paletteSidebar);
        UI.Workspace.AddChild(controlSidebar);
        UI.Workspace.AddChild(editDialog);
        UI.Workspace.AddChild(playBtn);
        UI.Workspace.AddChild(pauseBtn);
        UI.Workspace.AddChild(togglePlayPauseBtn);
        UI.Workspace.AddChild(stopBtn);
        UI.Workspace.AddChild(runButton);
        UI.Workspace.AddChild(waveButton);
        UI.Workspace.AddChild(navBar);

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

        PythonApi.OnSidebarToggled += (visible) =>
        {
            if (visible.HasValue) IsSidebarVisible = visible.Value;
            else ToggleSidebar();
            componentSidebar.Visible = IsSidebarVisible;
            paletteSidebar.Visible = IsSidebarVisible;
        };

        PythonApi.OnControlSidebarToggled += (visible) =>
        {
            if (visible.HasValue) IsControlSidebarVisible = visible.Value;
            else ToggleControlSidebar();
            controlSidebar.Visible = IsControlSidebarVisible;
        };

        PythonApi.OnSidebarVisibilityChanged += (name, visible) =>
        {
            if (name.Contains("Control", StringComparison.OrdinalIgnoreCase))
            {
                IsControlSidebarVisible = visible;
                controlSidebar.Visible = visible;
            }
            else
            {
                IsSidebarVisible = visible;
                componentSidebar.Visible = visible;
                paletteSidebar.Visible = visible;
            }
        };

        PythonApi.OnSimulationPlayRequested += () =>
        {
            if (!IsSimulationPlaying) ToggleSimulationPlayback();
        };

        PythonApi.OnSimulationPauseRequested += () =>
        {
            if (IsSimulationPlaying) ToggleSimulationPlayback();
        };

        PythonApi.OnSimulationToggleRequested += () => ToggleSimulationPlayback();

        PythonApi.OnSimulationStopRequested += () =>
        {
            if (IsSimulationPlaying) ToggleSimulationPlayback();
            _pendingSolverElapsedTimeSeconds = 0;
            _solverTimeSeconds = Preferences.Default.Get("Settings.SimulationStartTimeSeconds", 0.0);
            _deviceTimeSeconds = _solverTimeSeconds * (Preferences.Default.Get("Settings.EnableTimeScaling", false)
                ? GetConfiguredDeviceTimeScale() : 1.0);
            StatusText = "■ Simulation Stopped";
            CircuitChanged?.Invoke();
        };

        PythonApi.OnUndoRequested += () => Undo();
        PythonApi.OnRedoRequested += () => Redo();
        PythonApi.OnProjectNameChanged += name => ProjectName = name;
        PythonApi.OnWaveformDrawerToggleRequested += () => ToggleWaveformDrawer();
        PythonApi.OnViewToggleRequested += () => ToggleViewport();
        PythonApi.OnComponentRotateRequested += (name, deg) => CircuitChanged?.Invoke();
        PythonApi.OnComponentStretchRequested += (name, sx, sy) => CircuitChanged?.Invoke();
        PythonApi.OnLogicToggled += (name) => CircuitChanged?.Invoke();
        PythonApi.OnDescriptionSidebarToggled += () => ToggleDescriptionSidebar();
        PythonApi.OnComponentDeleted += (name) => CircuitChanged?.Invoke();
        PythonApi.OnComponentCopied += (name) => CircuitChanged?.Invoke();

        SaveDestinationFolder = GetDefaultSaveDirectory();
        PythonApi.OnSaveRequested += (path) =>
        {
            if (string.IsNullOrEmpty(path)) SaveProject();
            else { SaveToPath(path); CurrentFilePath = path; ProjectName = System.IO.Path.GetFileName(path); }
        };
        PythonApi.OnSaveAsRequested += (path, fmt) =>
        {
            SelectedSaveFormat = fmt;
            SaveToPath(path);
            CurrentFilePath = path;
            ProjectName = System.IO.Path.GetFileName(path);
        };

        _ = LoadCustomCircuitLibraryAsync();
    }



    // ── Commands ─────────────────────────────────────────────────────────────
    [RelayCommand]
    private void SelectTool(string tool) => ActiveTool = tool;

    [RelayCommand]
    private void RecenterViewport()
    {
        if (!Circuit.FindChildren<ComponentNode>().Any(component => component is not WireComponent))