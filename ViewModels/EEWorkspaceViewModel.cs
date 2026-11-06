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
    public partial bool IsAiEditMode { get; set; } = true;

    [ObservableProperty]
    public partial bool IsAwaitingAiReply { get; set; }

    private string _pendingAiPrompt = string.Empty;
    public string AiSchemaInputPlaceholder => IsAwaitingAiReply
        ? "Answer the AI clarification…"
        : "Ask AI to create or edit this circuit…";

    partial void OnIsAwaitingAiReplyChanged(bool value) => OnPropertyChanged(nameof(AiSchemaInputPlaceholder));

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
        new ComponentPaletteItem("VernierDiode",     "VD",  "Vf", "#FF9F43", "Electrical",  "Fine-adjustable/vernier junction diode", "vernier_diode.svg"),
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
            new ComponentPaletteItem("Diode",        "D",  "▷|","#FF7B72", "Diodes",  "Semiconductor diode",   "diode.svg"),
            new ComponentPaletteItem("VernierDiode", "VD", "Vf","#FF9F43", "Diodes",  "Fine-adjustable/vernier junction diode", "vernier_diode.svg"),
            new ComponentPaletteItem("LED",          "💡", "LED","#E3B341", "Diodes",  "Light Emitting Diode",  "led.svg"),
        }),
        new ComponentPaletteGroup("💡 Output & Logic I/O", new[]
        {
            new ComponentPaletteItem("Bulb",        "💡", "LAMP", "#F59E0B", "Output",  "Incandescent Light Bulb",   "bulb.svg"),
            new ComponentPaletteItem("LED",         "💡", "LED",  "#E3B341", "Output",  "Light Emitting Diode",      "led.svg"),
            new ComponentPaletteItem("LogicInput",  "🔲", "IN",   "#56D364", "Digital", "Toggle Logic Input (0/1)",  "logicinput.svg"),
            new ComponentPaletteItem("LogicOutput", "🎯", "OUT",  "#79C0FF", "Digital", "Digital Logic Probe (0/1)", "logicoutput.svg"),
            new ComponentPaletteItem("Pin",         "⚲",  "PIN",  "#58A6FF", "Digital", "Named I/O Port Terminal",   "pin.svg"),
            new ComponentPaletteItem("Probe",       "👁",  "PRB",  "#79C0FF", "Digital", "Voltage/Current Probe Point","probe.svg"),
            new ComponentPaletteItem("BitExtender", "⇄",  "EXT",  "#79C0FF", "Digital", "Bus Bit Extender",          "bitextender.svg"),
        }),
        new ComponentPaletteGroup("⚙ Switches", new[]
        {
            new ComponentPaletteItem("VoltageSwitch",   "S",  "SW", "#E3B341", "Switches", "Voltage-Controlled Switch", "voltageswitch.svg"),
            new ComponentPaletteItem("CurrentSwitch",   "W",  "SW", "#E3B341", "Switches", "Current-Controlled Switch", "currentswitch.svg"),
            new ComponentPaletteItem("Transistor",      "SW", "SWT","#FFA657", "Switches", "Digital Switch Transistor", "transistor_digital.svg"),
            new ComponentPaletteItem("TransmissionGate","TG", "TG", "#D2A8FF", "Switches", "CMOS Transmission Gate",    "transmissiongate.svg"),
        }),
        new ComponentPaletteGroup("📡 Transmission Lines", new[]
        {
            new ComponentPaletteItem("SinglePhaseTransmissionLine", "T1", "RLGC", "#79C0FF", "Transmission Lines", "Single-phase line: short, medium, or long-line model", "single_phase_line.svg"),
            new ComponentPaletteItem("TwoPhaseTransmissionLine", "T2", "RLGC", "#79C0FF", "Transmission Lines", "True two-phase or split-phase three-wire line", "two_phase_line.svg"),
            new ComponentPaletteItem("ThreePhaseTransmissionLine", "T3", "RLGC", "#79C0FF", "Transmission Lines", "Three-phase line with explicit phase/neutral nodes", "three_phase_line.svg"),
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
            new ComponentPaletteItem("ACBulb", "ACB", "W", "#F59E0B", "Electrical Loads", "Household AC bulb load", "ac_bulb.svg"),
            new ComponentPaletteItem("ACFan", "FAN", "W", "#79C0FF", "Electrical Loads", "Household AC fan motor load", "ac_fan.svg"),
        }),
        new ComponentPaletteGroup("🌞 Sources & Energy", new[]
        {
            new ComponentPaletteItem("SolarPanel", "PV", "Wp", "#E3B341", "Sources & Energy", "Single-diode photovoltaic panel", "solar_panel.svg"),
        }),
        new ComponentPaletteGroup("🔋 Energy Storage", new[]
        {
            new ComponentPaletteItem("Battery", "BAT", "Ah", "#56D364", "Energy Storage", "SOC-dependent battery with internal resistance", "battery.svg"),
        }),
        new ComponentPaletteGroup("🔌 Semiconductors", new[]
        {
            new ComponentPaletteItem("NPN",   "Q",  "NPN", "#FFA657", "Semi", "BJT NPN Transistor",    "bjt_npn.svg"),
            new ComponentPaletteItem("PNP",   "Q",  "PNP", "#FFA657", "Semi", "BJT PNP Transistor",    "bjt_pnp.svg"),
            new ComponentPaletteItem("JFET",  "J",  "FET", "#FFA657", "Semi", "Junction Field-Effect", "jfet.svg"),
            new ComponentPaletteItem("NMOS",  "M",  "NMOS","#D2A8FF", "Semi", "N-Channel MOSFET",     "nmos.svg"),
            new ComponentPaletteItem("PMOS",  "M",  "PMOS","#D2A8FF", "Semi", "P-Channel MOSFET",     "pmos.svg"),
            new ComponentPaletteItem("OpAmp", "▷",  "AMP", "#79C0FF", "Semi", "Operational Amplifier","opamp.svg"),
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
            .Select(part => new ComponentPaletteItem(part, part, "TTL", "#58A6FF", "TTL", $"74-series {part}, {TtlPackageComponent.GetPackagePinCount(part)}-pin DIP package", $"ttl_{part}.svg"))
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
        {
            StatusText = "No circuit components to center";
            return;
        }

        ViewportRecenterRequested?.Invoke();
        StatusText = "Centered on the circuit";
    }

    public void CommitComponentMove(ComponentNode component, double oldX, double oldY, double newX, double newY)
    {
        if (Math.Abs(oldX - newX) < 0.01 && Math.Abs(oldY - newY) < 0.01) return;

        component.X = oldX;
        component.Y = oldY;
        CommandManager.ExecuteCommand(new MoveComponentCommand(Circuit, component.Name, newX, newY)
        {
            Origin = CommandOrigin.Schematic
        });
        CircuitChanged?.Invoke();
    }

    [RelayCommand]
    private void ToggleHelp() => IsHelpVisible = !IsHelpVisible;

    [RelayCommand]
    private async Task OpenSettings()
    {
        try
        {
            await Shell.Current.GoToAsync("//settings");
            StatusText = "Settings opened";
        }
        catch (Exception ex)
        {
            StatusText = $"Could not open Settings: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task OpenAccount()
    {
        try
        {
            await Shell.Current.GoToAsync("account-detail");
            StatusText = "Account opened";
        }
        catch (Exception ex)
        {
            StatusText = $"Could not open Account: {ex.Message}";
        }
    }

    [RelayCommand]
    public async Task OpenExamplesPage()
    {
        try
        {
            await Shell.Current.GoToAsync("//examples");
        }
        catch (Exception ex)
        {
            StatusText = $"Could not open examples: {ex.Message}";
        }
    }

    public void LoadCircuit(CircuitNode newCircuit, string projectName, string? filePath = null)
    {
        if (newCircuit == null) return;

        // Clear existing circuit nodes
        while (Circuit.Children.Count > 0)
        {
            Circuit.RemoveChild(Circuit.Children[0]);
        }

        // Add children from loaded circuit
        foreach (var child in newCircuit.Children.ToList())
        {
            Circuit.AddChild(child);
        }

        Circuit.Title = newCircuit.Title;
        Circuit.Description = newCircuit.Description;
        ProjectName = projectName;
        CurrentFilePath = filePath;

        // Notify the script synchronizer so the Python script view reflects the loaded circuit (§5.A)
        _sync.OnCircuitReplaced(Circuit);
        ScriptText = _sync.CurrentScript;

        StatusText = $"Loaded circuit: {projectName}";
        CircuitChanged?.Invoke();
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
    public void SaveProject()
    {
#if ANDROID
    // Android exports route through Save As and MediaStore into Downloads/EEEngine.
        if (DeviceInfo.Platform == DevicePlatform.Android)
        {
            SaveProjectAs();
            return;
        }
#endif
        if (string.IsNullOrWhiteSpace(CurrentFilePath))
        {
            SaveProjectAs();
            return;
        }

        try
        {
            SaveToPath(CurrentFilePath);
            StatusText = $"Saved: {System.IO.Path.GetFileName(CurrentFilePath)}";
        }
        catch (Exception ex)
        {
            StatusText = $"Save error: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task SaveProjectToServerAsync()
    {
        try
        {
            StatusText = "Saving project to the server…";
            var result = await _remoteApi.SaveProjectAsync(Circuit, System.IO.Path.GetFileNameWithoutExtension(ProjectName));
            StatusText = string.IsNullOrWhiteSpace(result.Id)
                ? "Project saved to the server."
                : $"Project saved to the server (ID: {result.Id}).";
        }
        catch (Exception ex)
        {
            StatusText = $"Server save failed: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task SaveProjectAsOnlineAsync()
    {
        string title = System.IO.Path.GetFileNameWithoutExtension(SaveFileName).Trim();
        if (string.IsNullOrWhiteSpace(title))
        {
            StatusText = "Filename cannot be empty.";
            return;
        }

        try
        {
            StatusText = "Saving project online…";
            var result = await _remoteApi.SaveProjectAsync(Circuit, title);
            ProjectName = title + ".eee";
            IsSaveAsModalVisible = false;
            StatusText = string.IsNullOrWhiteSpace(result.Id)
                ? "Project saved online."
                : $"Project saved online (ID: {result.Id}).";
        }
        catch (Exception ex)
        {
            StatusText = $"Online save failed: {ex.Message}";
        }
    }

    [RelayCommand]
    public void SaveProjectAs()
    {
#if ANDROID
        if (DeviceInfo.Platform == DevicePlatform.Android)
            SaveDestinationFolder = "Downloads/EEEngine";
        else
#endif
        if (string.IsNullOrWhiteSpace(SaveDestinationFolder))
            SaveDestinationFolder = GetDefaultSaveDirectory();

        string currentName = !string.IsNullOrWhiteSpace(CurrentFilePath)
            ? System.IO.Path.GetFileNameWithoutExtension(CurrentFilePath)
            : (!string.IsNullOrWhiteSpace(ProjectName) ? System.IO.Path.GetFileNameWithoutExtension(ProjectName) : "Circuit1");

        SaveFileName = currentName;
        IsSaveAsModalVisible = true;
    }

    [RelayCommand]
    public void CloseSaveAsDialog()
    {
        IsSaveAsModalVisible = false;
    }

    [RelayCommand]
    public void SelectSaveFormat(string format)
    {
        SelectedSaveFormat = format;
    }

    [RelayCommand]
    public async Task ConfirmSaveAs()
    {
        if (string.IsNullOrWhiteSpace(SaveFileName))
        {
            StatusText = "Filename cannot be empty.";
            return;
        }

        string cleanName = System.IO.Path.GetFileNameWithoutExtension(SaveFileName).Trim();
        string ext = SelectedSaveFormat switch
        {
            "eee" => ".eee",
            "eesim" => ".eesim",
            "kicad" => ".kicad_sch",
            "spice" => ".cir",
            "python" => ".py",
            _ => ".eee"
        };

        if (string.IsNullOrWhiteSpace(SaveDestinationFolder))
            SaveDestinationFolder = GetDefaultSaveDirectory();

        string fullPath = System.IO.Path.Combine(SaveDestinationFolder, cleanName + ext);

        try
        {
#if ANDROID
            if (DeviceInfo.Platform == DevicePlatform.Android)
            {
                string temporaryPath = System.IO.Path.Combine(
                    FileSystem.CacheDirectory,
                    $"{Guid.NewGuid():N}{ext}");
                try
                {
                    SaveToPath(temporaryPath);
                    if (Android.OS.Build.VERSION.SdkInt >= Android.OS.BuildVersionCodes.Q)
                    {
                        await EEE.Platforms.Android.AndroidDownloadSaver.SaveAsync(temporaryPath, cleanName + ext);
                        StatusText = $"Saved to Downloads/EEEngine/{cleanName + ext}";
                    }
                    else
                    {
                        await using var stream = System.IO.File.OpenRead(temporaryPath);
                        var result = await FileSaver.Default.SaveAsync(
                            cleanName + ext,
                            stream,
                            System.Threading.CancellationToken.None);
                        if (!result.IsSuccessful)
                        {
                            StatusText = result.Exception?.Message ?? "Save cancelled.";
                            return;
                        }
                        StatusText = $"Saved locally: {result.FilePath}";
                    }

                    CurrentFilePath = null;
                    ProjectName = cleanName + ext;
                    IsSaveAsModalVisible = false;
                    return;
                }
                finally
                {
                    if (System.IO.File.Exists(temporaryPath))
                        System.IO.File.Delete(temporaryPath);
                }
            }
#endif
            SaveToPath(fullPath);
            CurrentFilePath = fullPath;
            ProjectName = System.IO.Path.GetFileName(fullPath);
            IsSaveAsModalVisible = false;
            StatusText = $"Saved: {System.IO.Path.GetFileName(fullPath)} ({SelectedSaveFormat.ToUpperInvariant()})";
        }
        catch (Exception ex)
        {
            StatusText = $"Save error: {ex.Message}";
        }
    }

    private string GetDefaultSaveDirectory()
    {
        try
        {
            // Use an app-owned location that is writable on Windows regardless of
            // Documents redirection, OneDrive policies, or the current working folder.
            string path = System.IO.Path.Combine(FileSystem.AppDataDirectory, "Projects");
            System.IO.Directory.CreateDirectory(path);
            return path;
        }
        catch
        {
            string fallback = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "EESimulator", "Projects");
            System.IO.Directory.CreateDirectory(fallback);
            return fallback;
        }
    }

    private void SaveToPath(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            throw new ArgumentException("Choose a valid project file path.", nameof(filePath));

        string fullPath = System.IO.Path.GetFullPath(filePath);
        string? dir = System.IO.Path.GetDirectoryName(fullPath);
        if (string.IsNullOrWhiteSpace(dir))
            throw new IOException("The selected path has no parent directory.");
        System.IO.Directory.CreateDirectory(dir);

        string ext = System.IO.Path.GetExtension(fullPath).ToLowerInvariant();
        string tempPath = System.IO.Path.Combine(dir,
            $".{System.IO.Path.GetFileNameWithoutExtension(fullPath)}.{Guid.NewGuid():N}.tmp{ext}");
        try
        {
            switch (ext)
            {
                case ".eee":
                    EeePlatformSerializer.SaveToFile(Circuit, tempPath);
                    break;
                case ".eesim":
                case ".json":
                    EesimSerializer.SaveToFile(Circuit, tempPath);
                    break;
                case ".kicad_sch":
                    KiCadExporter.SaveToFile(Circuit, tempPath);
                    break;
                case ".cir":
                case ".sp":
                    System.IO.File.WriteAllText(tempPath, SpiceNetlistExporter.ExportToSpice(Circuit, BuildSimulationSettings()));
                    break;
                case ".py":
                    System.IO.File.WriteAllText(tempPath, PythonCodeGenerator.GenerateFullScript(Circuit));
                    break;
                default:
                    EeePlatformSerializer.SaveToFile(Circuit, tempPath);
                    break;
            }

            System.IO.File.Move(tempPath, fullPath, overwrite: true);
        }
        finally
        {
            if (System.IO.File.Exists(tempPath))
                System.IO.File.Delete(tempPath);
        }
    }


    [RelayCommand]
    private void RotateSelectedComponent()
    {
        if (SelectedConnection != null)
        {
            RotateSelectedConnectionRoute();
            return;
        }
        var comp = SelectedComponent;
        if (comp == null)
        {
            StatusText = "No component selected — click a component first";
            return;
        }
        comp.Rotation = (comp.Rotation + 90.0) % 360.0;
        StatusText = $"Rotated {comp.Reference} → {comp.Rotation:F0}°";
        CircuitChanged?.Invoke();
    }

    [RelayCommand]
    private void EditSelectedComponent()
    {
        if (SelectedComponent != null)
            OpenEditDialog(SelectedComponent);
    }

    [RelayCommand]
    public void CopySelectedComponent()
    {
        if (SelectedComponent == null)
        {
            StatusText = "No component selected to copy";
            return;
        }

        var src = SelectedComponent;
        var copy = EESimulator.Core.Components.Registry.ComponentRegistry.CreateInstance(
            src.ComponentType,
            GetNextReference(src.ComponentType),
            src.X + 30,
            src.Y + 30);

        copy.Rotation = src.Rotation;
        copy.ScaleX = src.ScaleX;
        copy.ScaleY = src.ScaleY;
        copy.BreakingCurrent = src.BreakingCurrent;
        foreach (var kvp in src.ElectricalParameters)
        {
            copy.ElectricalParameters[kvp.Key] = kvp.Value;
        }
        foreach (var kvp in src.CustomProperties)
        {
            copy.CustomProperties[kvp.Key] = kvp.Value;
        }

        var cmd = new AddComponentCommand(Circuit, copy)
        {
            Origin = CommandOrigin.Schematic
        };
        CommandManager.ExecuteCommand(cmd);

        SelectedComponent = copy;
        OnComponentSelected(copy);
        CircuitChanged?.Invoke();
        StatusText = $"Copied {src.Reference} → {copy.Reference}";
    }

    [RelayCommand]
    public void DeleteSelectedComponent()
    {
        if (SelectedConnection != null)
        {
            var connection = SelectedConnection;
            Circuit.RemoveChild(connection);
            SelectedConnection = null;
            CircuitChanged?.Invoke();
            StatusText = $"Deleted wire {connection.NetName}";
            return;
        }
        if (SelectedComponent == null)
        {
            StatusText = "No component selected to delete";
            return;
        }

        var comp = SelectedComponent;
        var cmd = new RemoveComponentCommand(Circuit, comp)
        {
            Origin = CommandOrigin.Schematic
        };
        CommandManager.ExecuteCommand(cmd);

        SelectedComponent = null;
        SelectedConnection = null;
        CircuitChanged?.Invoke();
        StatusText = $"Deleted {comp.Reference}";
    }

    private void RotateSelectedConnectionRoute()
    {
        var connection = SelectedConnection;
        if (connection == null) return;
        var from = Circuit.FindChildById<ComponentNode>(connection.FromComponentId);
        var to = Circuit.FindChildById<ComponentNode>(connection.ToComponentId);
        var fromPin = from?.GetPin(connection.FromPin);
        var toPin = to?.GetPin(connection.ToPin);
        if (from == null || to == null || fromPin == null || toPin == null)
        {
            StatusText = "Cannot rotate this wire because one of its endpoint nodes is missing.";
            return;
        }

        static Point2D PinWorldPosition(ComponentNode component, ComponentPin pin)
        {
            double localX = pin.RelativeX * (component.ScaleX > 0 ? component.ScaleX : 1.0);
            double localY = pin.RelativeY * (component.ScaleY > 0 ? component.ScaleY : 1.0);
            double radians = component.Rotation * Math.PI / 180.0;
            return new Point2D(
                component.X + localX * Math.Cos(radians) - localY * Math.Sin(radians),
                component.Y + localX * Math.Sin(radians) + localY * Math.Cos(radians));
        }
        var fromPosition = PinWorldPosition(from, fromPin);
        var toPosition = PinWorldPosition(to, toPin);
        double x1 = fromPosition.X;
        double y1 = fromPosition.Y;
        double x2 = toPosition.X;
        double y2 = toPosition.Y;
        double centerX = (x1 + x2) / 2.0;
        double centerY = (y1 + y2) / 2.0;
        if (connection.RoutingPoints.Count == 0)
        {
            double middleX = (x1 + x2) / 2.0;
            connection.RoutingPoints.Add(new Point2D(middleX, y1));
            connection.RoutingPoints.Add(new Point2D(middleX, y2));
        }

        for (int index = 0; index < connection.RoutingPoints.Count; index++)
        {
            var point = connection.RoutingPoints[index];
            double dx = point.X - centerX;
            double dy = point.Y - centerY;
            connection.RoutingPoints[index] = new Point2D(centerX - dy, centerY + dx);
        }
        connection.NotifyModified(nameof(connection.RoutingPoints));
        CircuitChanged?.Invoke();
        StatusText = $"Rotated wire route {connection.NetName} 90°; endpoint nodes remain connected";
    }

    [RelayCommand]
    public void ToggleDescriptionSidebar()
    {
        IsDescriptionSidebarVisible = !IsDescriptionSidebarVisible;
        if (IsDescriptionSidebarVisible && SelectedComponent != null)
        {
            OnComponentSelected(SelectedComponent);
        }
    }

    [RelayCommand]
    public void ToggleLiveMeasurements()
    {
        IsLiveMeasurementsVisible = !IsLiveMeasurementsVisible;
        StatusText = IsLiveMeasurementsVisible
            ? "Live voltage and current overlays enabled"
            : "Live voltage and current overlays hidden";
        CircuitChanged?.Invoke();
    }

    [RelayCommand]
    public void ToggleSelectedWaveform()
    {
        if (SelectedConnection != null)
        {
            var fromComponent = Circuit.FindChildById<ComponentNode>(SelectedConnection.FromComponentId);
            var toComponent = Circuit.FindChildById<ComponentNode>(SelectedConnection.ToComponentId);
            var wireSignals = new[] { fromComponent, toComponent }.Where(source => source != null).Cast<ComponentNode>().Distinct().ToList();
            if (wireSignals.Any(source => source.ShowWaveform))
            {
                foreach (var source in wireSignals) source.ShowWaveform = false;
                IsWaveformDrawerOpen = false;
                CircuitChanged?.Invoke();
                return;
            }
            PrepareConnectionWaveform(SelectedConnection);
            return;
        }

        var component = SelectedComponent;
        if (component == null) return;

        if (component.ShowWaveform)
        {
            component.ShowWaveform = false;
            if (WaveformComponent == component)
                IsWaveformDrawerOpen = false;
            StatusText = $"Waveform disabled for {component.Reference}";
        }
        else
        {
            ConfigureWaveformForComponent(component, showTerminalPicker: true);
        }
        CircuitChanged?.Invoke();
    }

    private void ConfigureWaveformForComponent(ComponentNode component, bool showTerminalPicker)
    {
        WaveformSignals.Clear();
        WaveformTracePickerComponent = component;

        if (component.Pins.Count > 2)
        {
            foreach (var pin in component.Pins)
                WaveformSignals.Add(new WaveformSignal(component, pin.Id,
                    $"{pin.Name} ({pin.Id}) voltage", WaveformSignalKind.PinVoltage));

            if (showTerminalPicker)
            {
                IsWaveformTracePickerVisible = true;
                return;
            }
        }
        else if (component.Pins.Count == 2)
        {
            WaveformSignals.Add(new WaveformSignal(component, string.Empty,
                "Component voltage", WaveformSignalKind.ComponentVoltage));
            WaveformSignals.Add(new WaveformSignal(component, string.Empty,
                "Component current", WaveformSignalKind.ComponentCurrent));
        }
        else if (component.Pins.Count == 1)
        {
            var pin = component.Pins[0];
            WaveformSignals.Add(new WaveformSignal(component, pin.Id,
                $"{pin.Name} ({pin.Id}) voltage", WaveformSignalKind.PinVoltage));
        }
        else
        {
            WaveformSignals.Add(new WaveformSignal(component, string.Empty,
                "Component voltage", WaveformSignalKind.ComponentVoltage));
            WaveformSignals.Add(new WaveformSignal(component, string.Empty,
                "Component current", WaveformSignalKind.ComponentCurrent));
        }

        EnableWaveformForComponent(component);
    }

    private void PrepareConnectionWaveform(ConnectionNode connection)
    {
        var from = Circuit.FindChildById<ComponentNode>(connection.FromComponentId);
        var to = Circuit.FindChildById<ComponentNode>(connection.ToComponentId);
        var fromPin = from?.GetPin(connection.FromPin);
        var toPin = to?.GetPin(connection.ToPin);
        if (from == null || to == null || fromPin == null || toPin == null)
        {
            StatusText = "Cannot plot this wire because an endpoint node is missing.";
            return;
        }

        WaveformSignals.Clear();
        WaveformTracePickerComponent = from;
        WaveformSignals.Add(new WaveformSignal(from, fromPin.Id, $"{connection.NetName}: {from.Reference}:{fromPin.Name}", WaveformSignalKind.PinVoltage));
        WaveformSignals.Add(new WaveformSignal(to, toPin.Id, $"{connection.NetName}: {to.Reference}:{toPin.Name}", WaveformSignalKind.PinVoltage));
        IsWaveformTracePickerVisible = true;
    }

    private void EnableWaveformForComponent(ComponentNode component)
    {
        component.ShowWaveform = true;
        foreach (var source in WaveformSignals.Where(signal => signal.IsSelected).Select(signal => signal.Component).Distinct())
            source.ShowWaveform = true;
        WaveformComponent = component;
        IsWaveformDrawerOpen = true;
        IsWaveformTracePickerVisible = false;
        StatusText = $"Waveform enabled for {component.Reference}: {WaveformSignals.Count(signal => signal.IsSelected)} trace(s)";
        CircuitChanged?.Invoke();
        RequestSimulationRefresh();
    }

    [RelayCommand]
    public void SetAllWaveformSignals(object? parameter)
    {
        bool selected = parameter switch
        {
            bool b => b,
            string str => bool.TryParse(str, out var parsed) && parsed,
            _ => true
        };
        foreach (var signal in WaveformSignals)
            signal.IsSelected = selected;
    }

    [RelayCommand]
    public void ConfirmWaveformTraceSelection()
    {
        var component = WaveformTracePickerComponent;
        if (component == null) return;
        if (!WaveformSignals.Any(signal => signal.IsSelected))
        {
            StatusText = "Select at least one terminal waveform.";
            return;
        }
        EnableWaveformForComponent(component);
    }

    [RelayCommand]
    public void CancelWaveformTraceSelection() => IsWaveformTracePickerVisible = false;

    [RelayCommand]
    public void ApplyDescriptionSidebarChanges()
    {
        if (SelectedComponent == null) return;
        if (!string.IsNullOrWhiteSpace(EditName))
        {
            SelectedComponent.Name = EditName.Trim();
            SelectedComponent.Reference = EditName.Trim();
        }
        SelectedComponent.CustomProperties["Description"] = EditDescription;
        SelectedComponent.BreakingCurrent = Math.Max(EditBreakingCurrent, 0.0);
        if (!string.IsNullOrEmpty(EditParamName))
        {
            SelectedComponent.ElectricalParameters[EditParamName] = EditParamValue;
        }
        SelectedComponent.ShowLiveProbe = EditShowLiveProbe;
        SelectedComponent.ShowWaveform = EditShowWaveform;
        SelectedComponent.NotifyModified(SelectedComponent.Reference);
        CircuitChanged?.Invoke();
        RequestSimulationRefresh();
        StatusText = $"Updated description & properties for {SelectedComponent.Reference}";
    }

    [RelayCommand]
    public void OpenCreateCircuitComponent()
    {
        if (SelectedComponent == null)
        {
            StatusText = "Select a component in the circuit you want to package first.";
            return;
        }

        var island = FindConnectedCircuitIsland(SelectedComponent);
        if (island.Count < 2)
        {
            StatusText = "A reusable circuit component needs a connected circuit with at least two components.";
            return;
        }
        if (island.Any(component => component is CustomCircuitComponent))
        {
            StatusText = "Nested reusable circuit components are not supported. Expand the existing custom component first.";
            return;
        }

        var islandIds = island.Select(component => component.Id).ToHashSet();
        var connectedPins = Circuit.Connections
            .Where(connection => islandIds.Contains(connection.FromComponentId) && islandIds.Contains(connection.ToComponentId))
            .SelectMany(connection => new[]
            {
                (connection.FromComponentId, connection.FromPin),
                (connection.ToComponentId, connection.ToPin)
            }).ToHashSet();
        foreach (var tunnelGroup in island.OfType<TunnelComponent>().GroupBy(tunnel => tunnel.TunnelLabel, StringComparer.OrdinalIgnoreCase))
        {
            if (tunnelGroup.Count() < 2) continue;
            foreach (var tunnel in tunnelGroup)
                if (tunnel.Pins.Count > 0) connectedPins.Add((tunnel.Id, tunnel.Pins[0].Id));
        }

        var openPins = island.SelectMany(component => component.Pins
                .Where(pin => !connectedPins.Contains((component.Id, pin.Id)))
                .Select(pin => (Component: component, Pin: pin)))
            .OrderBy(item => item.Component.Reference, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.Pin.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (openPins.Count == 0)
        {
            StatusText = "This circuit has no open pins to expose. Leave at least one terminal open before packaging it.";
            return;
        }

        NewCircuitComponentPorts.Clear();
        for (int index = 0; index < openPins.Count; index++)
        {
            var item = openPins[index];
            bool leftSide = index < (openPins.Count + 1) / 2;
            int row = leftSide ? index : index - (openPins.Count + 1) / 2;
            NewCircuitComponentPorts.Add(new CustomCircuitPortDraft(
                item.Component.Id, item.Pin.Id, item.Component.Reference, item.Pin.Name,
                $"N{index + 1}", leftSide ? -28 : 28, (row - (openPins.Count / 4)) * 14));
        }

        NewCircuitComponentName = string.IsNullOrWhiteSpace(SelectedComponent.Reference)
            ? "CustomCircuit"
            : $"{SelectedComponent.Reference}Module";
        NewCircuitComponentSvg = "<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 64 48\"><rect x=\"4\" y=\"4\" width=\"56\" height=\"40\" rx=\"6\" fill=\"#161B22\" stroke=\"#58A6FF\" stroke-width=\"3\"/><path d=\"M0 16h10M54 32h10\" stroke=\"#58A6FF\" stroke-width=\"3\"/></svg>";
        CreateCircuitComponentStatus = $"{island.Count} connected components; {openPins.Count} open pins will become named component nodes.";
        IsCreateCircuitComponentDialogVisible = true;
    }

    [RelayCommand]
    public void CancelCreateCircuitComponent() => IsCreateCircuitComponentDialogVisible = false;

    [RelayCommand]
    private Task CreateSelectedCircuitComponentLocallyAsync()
        => CreateSelectedCircuitComponentAsync(publishOnline: false);

    [RelayCommand]
    private Task CreateSelectedCircuitComponentAndPublishAsync()
        => CreateSelectedCircuitComponentAsync(publishOnline: true);

    public async Task CreateSelectedCircuitComponentAsync(bool publishOnline)
    {
        if (SelectedComponent == null) return;
        string name = NewCircuitComponentName.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            CreateCircuitComponentStatus = "Enter a component name.";
            return;
        }
        if (NewCircuitComponentPorts.Count == 0 || NewCircuitComponentPorts.Any(port => string.IsNullOrWhiteSpace(port.Name)))
        {
            CreateCircuitComponentStatus = "Every exposed terminal needs a name.";
            return;
        }
        if (NewCircuitComponentPorts.Select(port => port.Name.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).Count() != NewCircuitComponentPorts.Count)
        {
            CreateCircuitComponentStatus = "Terminal names must be unique.";
            return;
        }

        try
        {
            var island = FindConnectedCircuitIsland(SelectedComponent);
            var islandIds = island.Select(component => component.Id).ToHashSet();
            var subCircuit = new CircuitNode { Title = name, Description = "Reusable EESimulator circuit component" };
            foreach (var component in island)
            {
                var clone = component.Clone();
                clone.Id = component.Id;
                subCircuit.AddChild(clone);
            }
            var internalConnections = Circuit.Connections
                .Where(connection => islandIds.Contains(connection.FromComponentId) && islandIds.Contains(connection.ToComponentId))
                .ToList();
            foreach (var connection in internalConnections)
            {
                var clone = new ConnectionNode(connection.FromComponentId, connection.FromPin,
                    connection.ToComponentId, connection.ToPin, connection.NetName)
                {
                    Id = connection.Id,
                    Resistance = connection.Resistance
                };
                foreach (var point in connection.RoutingPoints)
                    clone.RoutingPoints.Add(new Point2D(point.X, point.Y));
                subCircuit.AddChild(clone);
            }

            string definitionId = Guid.NewGuid().ToString("N");
            var ports = NewCircuitComponentPorts.Select(port => new CustomCircuitPort
            {
                Id = $"port_{Guid.NewGuid():N}",
                Name = port.Name.Trim(),
                InnerComponentId = port.InnerComponentId,
                InnerPinId = port.InnerPinId,
                RelativeX = port.RelativeX,
                RelativeY = port.RelativeY
            }).ToList();
            subCircuit.Description = CustomCircuitLibrary.AddMetadata(subCircuit.Description, definitionId, ports);
            var definition = new CustomCircuitDefinition
            {
                Id = definitionId,
                Name = name,
                SvgIcon = NewCircuitComponentSvg,
                CircuitJson = EeePlatformSerializer.ExportToEee(subCircuit),
                Ports = ports
            };

            await CustomCircuitLibrary.SaveAsync(definition);
            _customCircuitDefinitions[definition.Id] = definition;
            RefreshCustomCircuitPalette();

            string onlineStatus = string.Empty;
            if (publishOnline)
            {
                try
                {
                    if (!_remoteApi.IsConfigured)
                        throw new InvalidOperationException("Configure the HCVP HTTPS API URL in Settings. AI drafts do not require sign-in or an app token.");
                    await _remoteApi.PublishExampleAsync(subCircuit, name, "Custom Components", definition.SvgIcon);
                    onlineStatus = " Published online.";
                }
                catch (Exception ex)
                {
                    onlineStatus = $" Saved locally; online publish failed: {ex.Message}";
                }
            }

            double x = island.Average(component => component.X);
            double y = island.Average(component => component.Y);
            foreach (var connection in Circuit.Connections.Where(connection =>
                         islandIds.Contains(connection.FromComponentId) || islandIds.Contains(connection.ToComponentId)).ToList())
                Circuit.RemoveChild(connection);
            foreach (var component in island)
                Circuit.RemoveChild(component);

            var macroReference = GetNextReference("CustomCircuit");
            var macro = new CustomCircuitComponent
            {
                Name = macroReference,
                Reference = macroReference,
                X = x,
                Y = y
            };
            macro.SetDefinition(definition);
            var addCommand = new AddComponentCommand(Circuit, macro) { Origin = CommandOrigin.Schematic };
            CommandManager.ExecuteCommand(addCommand);
            SelectedComponent = macro;
            IsCreateCircuitComponentDialogVisible = false;
            OnComponentSelected(macro);
            StatusText = $"Created reusable component '{name}'. Saved locally.{onlineStatus}";
            CircuitChanged?.Invoke();
        }
        catch (Exception ex)
        {
            CreateCircuitComponentStatus = $"Could not create component: {ex.Message}";
        }
    }

    private List<ComponentNode> FindConnectedCircuitIsland(ComponentNode selected)
    {
        var components = Circuit.Components.ToList();
        var adjacency = components.ToDictionary(component => component.Id, _ => new HashSet<Guid>());
        foreach (var connection in Circuit.Connections)
        {
            if (adjacency.ContainsKey(connection.FromComponentId) && adjacency.ContainsKey(connection.ToComponentId))
            {
                adjacency[connection.FromComponentId].Add(connection.ToComponentId);
                adjacency[connection.ToComponentId].Add(connection.FromComponentId);
            }
        }
        foreach (var group in components.OfType<TunnelComponent>().GroupBy(tunnel => tunnel.TunnelLabel, StringComparer.OrdinalIgnoreCase))
        {
            var ids = group.Select(tunnel => tunnel.Id).ToArray();
            for (int i = 1; i < ids.Length; i++)
            {
                adjacency[ids[0]].Add(ids[i]);
                adjacency[ids[i]].Add(ids[0]);
            }
        }
        var groundIds = components.Where(component => component is GroundNode).Select(component => component.Id).ToArray();
        for (int i = 1; i < groundIds.Length; i++)
        {
            adjacency[groundIds[0]].Add(groundIds[i]);
            adjacency[groundIds[i]].Add(groundIds[0]);
        }

        var visited = new HashSet<Guid> { selected.Id };
        var queue = new Queue<Guid>();
        queue.Enqueue(selected.Id);
        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            foreach (var neighbor in adjacency[current])
                if (visited.Add(neighbor)) queue.Enqueue(neighbor);
        }
        return components.Where(component => visited.Contains(component.Id)).ToList();
    }

    private async Task LoadCustomCircuitLibraryAsync()
    {
        var definitions = await CustomCircuitLibrary.LoadAsync();
        foreach (var definition in definitions)
            _customCircuitDefinitions[definition.Id] = definition;

        if (_remoteApi.IsConfigured)
        {
            try
            {
                var online = await _remoteApi.GetCommunityExamplesAsync();
                foreach (var example in online.Where(example => example.Category.Equals("Custom Components", StringComparison.OrdinalIgnoreCase)))
                {
                    var definition = CustomCircuitLibrary.TryReadRemoteDefinition(example);
                    if (definition == null) continue;
                    _customCircuitDefinitions[definition.Id] = definition;
                    await CustomCircuitLibrary.SaveAsync(definition);
                }
            }
            catch { /* Local custom components remain available while the optional API is offline. */ }
        }
        RefreshCustomCircuitPalette();
    }

    private void RefreshCustomCircuitPalette()
    {
        var existing = GroupedPalette.FirstOrDefault(group => group.Header == "🧩 My Circuits");
        if (existing != null) GroupedPalette.Remove(existing);
        var items = _customCircuitDefinitions.Values.OrderBy(definition => definition.Name, StringComparer.OrdinalIgnoreCase)
            .Select(definition => new ComponentPaletteItem(
                $"custom:{definition.Id}", "⌗", "MAC", "#D2A8FF", "My Circuits",
                $"Reusable circuit with {definition.Ports.Count} exposed nodes", string.Empty))
            .ToList();
        if (items.Count > 0) GroupedPalette.Add(new ComponentPaletteGroup("🧩 My Circuits", items));
    }

    [RelayCommand]
    private void ToggleViewport()
    {
        if (IsSchematicViewActive)
        {
            IsScriptViewActive = true;
            IsAnalyticsViewActive = false;
            UI.Workspace.SetViewport("Script");
        }
        else if (IsScriptViewActive)
        {
            IsScriptViewActive = false;
            IsAnalyticsViewActive = true;
            UI.Workspace.SetViewport("Analytics");
            _ = RunAnalyticsAsync();
        }
        else
        {
            IsAnalyticsViewActive = false;
            IsScriptViewActive = false;
            UI.Workspace.SetViewport("Schematic");
        }
    }

    [RelayCommand]
    public async Task RunAnalyticsAsync()
    {
        if (IsAnalyticsRunning) return;
        IsAnalyticsRunning = true;
        AnalyticsOutput = "Running circuit analysis…";
        try
        {
            AnalyticsOutput = await Task.Run(() =>
            {
                var result = _simManager.Run(Circuit, new SimulationSettings());
                _lastSimulationCircuitSnapshot = EeePlatformSerializer.ExportToEee(Circuit);
                return CircuitAnalyticsService.Analyze(Circuit, result, SelectedAnalyticsMode, SelectedComponent);
            });
            DispatchTriggeredUiEvents();
            OnPropertyChanged(nameof(SelectedComponent));
        }
        catch (Exception ex)
        {
            AnalyticsOutput = $"Analysis failed: {ex.Message}";
        }
        finally
        {
            IsAnalyticsRunning = false;
        }
    }

    [RelayCommand]
    private void ToggleSidebar()
    {
        IsSidebarVisible = !IsSidebarVisible;
        // When component sidebar opens, close control sidebar (all platforms)
        if (IsSidebarVisible)
            IsControlSidebarVisible = false;
    }

    [RelayCommand]
    private void ToggleControlSidebar()
    {
        IsControlSidebarVisible = !IsControlSidebarVisible;
        // When control sidebar opens, close component sidebar (all platforms)
        if (IsControlSidebarVisible)
            IsSidebarVisible = false;
    }

    private Microsoft.Maui.Dispatching.IDispatcherTimer? _animTimer;
    private bool _playbackSolveRunning;
    private bool _simulationRefreshRequested;
    private bool _isApplyingScript;
    private DateTime _lastPlaybackSolveUtc = DateTime.MinValue;
    private double _pendingSolverElapsedTimeSeconds;
    private double _solverTimeSeconds;
    private double _deviceTimeSeconds;

    [RelayCommand]
    public void ToggleSimulationPlayback()
    {
        IsSimulationPlaying = !IsSimulationPlaying;
        if (IsSimulationPlaying)
        {
            if (_solverTimeSeconds <= 0)
            {
                _solverTimeSeconds = Preferences.Default.Get("Settings.SimulationStartTimeSeconds", 0.0);
                _deviceTimeSeconds = _solverTimeSeconds * GetConfiguredDeviceTimeScale();
            }
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
                            double solverDelta = _animTimer.Interval.TotalSeconds;
                            _pendingSolverElapsedTimeSeconds += solverDelta;
                            _solverTimeSeconds += solverDelta;
                            double scale = GetConfiguredDeviceTimeScale();
                            _deviceTimeSeconds += solverDelta * scale;
                            DigitalLogicSimulator.Advance(Circuit, solverDelta);
                            ElectricityAnimPhase = (ElectricityAnimPhase + 0.05f) % 1.0f;
                            CircuitChanged?.Invoke();
                            if (!IsSimulating && !_playbackSolveRunning &&
                                DateTime.UtcNow - _lastPlaybackSolveUtc >= TimeSpan.FromMilliseconds(250))
                                _ = RefreshPlaybackSimulationAsync();
                        }
                    };
                }
            }
            _animTimer?.Start();
            StatusText = "▶ Simulation Running: Electricity flowing along circuits";
            _ = SimulateAsync();
        }

        else
        {
            _animTimer?.Stop();
            StatusText = "⏸ Simulation Paused";
        }
        CircuitChanged?.Invoke();
    }

    private async Task RefreshPlaybackSimulationAsync()
    {
        if (_playbackSolveRunning || IsSimulating || !IsSimulationPlaying)
        {
            _simulationRefreshRequested = true;
            return;
        }
        _simulationRefreshRequested = false;
        _playbackSolveRunning = true;
        IsSimulating = true;
        _lastPlaybackSolveUtc = DateTime.UtcNow;
        try
        {
            var settings = BuildSimulationSettings();
            settings.SolverElapsedTimeSeconds = _pendingSolverElapsedTimeSeconds;
            _pendingSolverElapsedTimeSeconds = 0;
            settings.DeviceTimeSeconds = _deviceTimeSeconds;
            settings.CaptureAllWaveforms = true;
            settings.StopTime = _solverTimeSeconds;
            settings.StartTime = Math.Max(0, settings.StopTime - settings.SolverElapsedTimeSeconds);
            if (settings.StopTime <= settings.StartTime)
            {
                double waveformWindow = Math.Max(settings.StepTime * 2, 0.02);
                settings.StartTime = Math.Max(0, settings.StopTime - waveformWindow);
            }
            settings.StepTime = Math.Max(settings.StepTime,
                Math.Max(settings.StopTime - settings.StartTime, 0.02) / 500.0);
            var results = await Task.Run(() => _simManager.Run(Circuit, settings));
            _lastSimulationCircuitSnapshot = EeePlatformSerializer.ExportToEee(Circuit);
            ConsoleOutput = FormatResults(results);
            DispatchTriggeredUiEvents();
            OnPropertyChanged(nameof(SelectedComponent));
            CircuitChanged?.Invoke();
        }
        catch (Exception ex)
        {
            ConsoleOutput = $"Live simulation failed: {ex.Message}";
        }
        finally
        {
            _playbackSolveRunning = false;
            IsSimulating = false;
            if (_simulationRefreshRequested)
            {
                _simulationRefreshRequested = false;
                if (IsSimulationPlaying)
                    _ = RefreshPlaybackSimulationAsync();
                else
                    _ = SimulateAsync();
            }
        }
    }

    private void RequestSimulationRefresh()
    {
        _simulationRefreshRequested = true;
        if (_isApplyingScript) return;
        if (IsSimulationPlaying)
        {
            if (!_playbackSolveRunning && !IsSimulating)
                _ = RefreshPlaybackSimulationAsync();
            return;
        }
        _ = SimulateAsync();
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
    private async Task GenerateSchemaScriptAsync()
    {
        if (IsAiSchemaRequestInProgress) return;
        bool answeringClarification = IsAwaitingAiReply;
        string requestPrompt = answeringClarification ? _pendingAiPrompt : AiSchemaPrompt.Trim();
        string clarificationAnswer = answeringClarification ? AiSchemaPrompt.Trim() : string.Empty;
        if (string.IsNullOrWhiteSpace(requestPrompt) || (answeringClarification && string.IsNullOrWhiteSpace(clarificationAnswer)))
        {
            AiSchemaStatus = answeringClarification
                ? "Enter an answer to the AI clarification first."
                : "Describe the schematic change you want first.";
            return;
        }

        try
        {
            IsAiSchemaRequestInProgress = true;
            AiSchemaStatus = "Generating a script draft…";
            string currentCircuitSnapshot = EeePlatformSerializer.ExportToEee(Circuit);
            if (Circuit.Components.Any() &&
                (_simManager.LastResults == null || !string.Equals(_lastSimulationCircuitSnapshot, currentCircuitSnapshot, StringComparison.Ordinal)))
            {
                AiSchemaStatus = "Measuring the current schematic…";
                await Task.Run(() => _simManager.Run(Circuit, new SimulationSettings()));
                _lastSimulationCircuitSnapshot = EeePlatformSerializer.ExportToEee(Circuit);
                DispatchTriggeredUiEvents();
            }
            var draft = await _remoteApi.GenerateSchemaScriptAsync(
                requestPrompt,
                ScriptText,
                ConsoleOutput,
                Circuit,
                _simManager.LastResults,
                IsAiEditMode,
                clarificationAnswer,
                answeringClarification);
            if (!answeringClarification && draft.Questions.Count > 0 && string.IsNullOrWhiteSpace(draft.Script))
            {
                _pendingAiPrompt = requestPrompt;
                IsAwaitingAiReply = true;
                AiSchemaPrompt = string.Empty;
                AiSchemaStatus = $"AI needs one clarification: {draft.Questions[0]} Enter your reply and submit once; it will then return a script without another question.";
                return;
            }
            if (answeringClarification)
            {
                IsAwaitingAiReply = false;
                _pendingAiPrompt = string.Empty;
                if (string.IsNullOrWhiteSpace(draft.Script))
                {
                    AiSchemaPrompt = string.Empty;
                    AiSchemaStatus = "The AI did not return a script after your answer. Start a new request with any missing details included.";
                    return;
                }
            }
            if (draft.Script.Trim().Length == 0)
            {
                AiSchemaStatus = draft.Questions.FirstOrDefault() ?? "The AI service returned an empty script draft.";
                return;
            }

            string normalizedScript = NormalizeAiScriptDraft(draft.Script);
            if (string.IsNullOrWhiteSpace(normalizedScript))
            {
                AiSchemaStatus = string.Join(" ", draft.Questions.Count > 0
                    ? draft.Questions
                    : new[] { "The AI service returned an empty script draft." });
                return;
            }

            await Microsoft.Maui.ApplicationModel.MainThread.InvokeOnMainThreadAsync(() =>
            {
                ScriptText = normalizedScript;
                AiSchemaPrompt = string.Empty;
                IsAnalyticsViewActive = false;
                IsScriptViewActive = true;
                UI.Workspace.SetViewport("Script");
                OnPropertyChanged(nameof(ScriptText));
                OnPropertyChanged(nameof(IsScriptViewActive));
            });

            var draftNotes = new List<string>();
            if (!string.IsNullOrWhiteSpace(draft.Summary)) draftNotes.Add(draft.Summary);
            if (draft.Warnings.Count > 0) draftNotes.Add("Warnings: " + string.Join("; ", draft.Warnings));
            draftNotes.Add("Review the draft before Run.");
            AiSchemaStatus = string.Join(" ", draftNotes);
        }
        catch (Exception ex)
        {
            AiSchemaStatus = $"AI request failed: {ex.Message}";
        }
        finally
        {
            IsAiSchemaRequestInProgress = false;
        }
    }

    private static string NormalizeAiScriptDraft(string? script)
    {
        if (string.IsNullOrWhiteSpace(script)) return string.Empty;

        var normalized = script.Trim().Replace("\r\n", "\n");
        if (!normalized.StartsWith("```", StringComparison.Ordinal)) return normalized;

        int firstLineEnd = normalized.IndexOf('\n');
        if (firstLineEnd > 0)
            normalized = normalized[(firstLineEnd + 1)..];
        if (normalized.EndsWith("```", StringComparison.Ordinal))
            normalized = normalized[..^3].TrimEnd();

        return normalized.Trim();
    }

    [RelayCommand]
    private async Task RunScriptAsync()
    {
        if (IsSimulating || _playbackSolveRunning)
        {
            StatusText = "Wait for the current simulation step to finish before running a script.";
            return;
        }

        _isApplyingScript = true;
        try
        {
            StatusText    = "Running script…";
            ConsoleOutput = "";
            bool scriptHadErrors = false;

            foreach (var rawLine in ScriptText.Split('\n'))
            {
                var line = rawLine.Trim();
                if (string.IsNullOrEmpty(line) || line.StartsWith('#'))
                    continue;

                ConsoleOutput += $">>> {line}\n";

                // Parse "eesim.xxx(...)" calls and dispatch to PythonApi
                var result = DispatchApiCall(line);
                if (result != null)
                {
                    ConsoleOutput += result + "\n";
                    if (result.StartsWith("[error]", StringComparison.OrdinalIgnoreCase) ||
                        result.StartsWith("[unknown", StringComparison.OrdinalIgnoreCase))
                        scriptHadErrors = true;
                }
            }

            var commandOutput = ConsoleOutput;
            _simulationRefreshRequested = false;
            IsSimulating = true;
            StatusText = scriptHadErrors
                ? "Script had errors. Simulating the resulting circuit state for AI feedback…"
                : "Changes applied. Running operating-point simulation for AI feedback…";
            var feedback = await Task.Run(() => _simManager.Run(Circuit, new SimulationSettings()));
            _lastSimulationCircuitSnapshot = EeePlatformSerializer.ExportToEee(Circuit);
            ConsoleOutput = commandOutput + "\n" + FormatResults(feedback);
            DispatchTriggeredUiEvents();
            OnPropertyChanged(nameof(SelectedComponent));
            AiSchemaStatus = feedback.Success
                ? scriptHadErrors
                    ? "Some script commands failed. Simulation feedback reflects the resulting partial state; the next AI request includes the errors and measurements."
                    : "Simulation feedback refreshed. The next AI request includes per-component measurements."
                : $"Simulation feedback unavailable: {feedback.Message}";
            StatusText = feedback.Success
                ? scriptHadErrors ? "Script partially applied; simulation complete" : "Script applied and simulated ✓"
                : scriptHadErrors ? "Script had errors; simulation failed" : "Script applied; simulation failed";
            CircuitChanged?.Invoke();
        }
        catch (Exception ex)
        {
            ConsoleOutput += $"ERROR: {ex.Message}\n";
            StatusText = "Script error";
        }
        finally
        {
            IsSimulating = false;
            OnPropertyChanged(nameof(SelectedComponent));
            _isApplyingScript = false;
            if (_simulationRefreshRequested)
            {
                _simulationRefreshRequested = false;
                if (IsSimulationPlaying)
                    _ = RefreshPlaybackSimulationAsync();
                else
                    _ = SimulateAsync();
            }
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

        double scale = GetConfiguredDeviceTimeScale();
        bool scaleEnabled = Preferences.Default.Get("Settings.EnableTimeScaling", false);
        bool automaticStep = Preferences.Default.Get("Settings.AutomaticTimeStepSelection", true);
        double configuredStart = Preferences.Default.Get("Settings.SimulationStartTimeSeconds", 0.0);
        double configuredDuration = Math.Max(Preferences.Default.Get("Settings.SimulationDurationSeconds", 60.0), 1e-6);
        double configuredStep = Math.Max(Preferences.Default.Get("Settings.SimulationTimeStepSeconds", 0.04), 1e-6);
        double solverStart = scaleEnabled ? configuredStart : SimTransientStartTime;
        double solverStop = scaleEnabled ? configuredStart + configuredDuration :
            (SimTransientStopTime > SimTransientStartTime ? SimTransientStopTime : SimTransientStartTime + 0.01);
        double solverStep = scaleEnabled ? configuredStep : (SimTransientStepTime > 0 ? SimTransientStepTime : 1e-5);
        if (automaticStep)
            solverStep = Math.Min(solverStep, Math.Max((solverStop - solverStart) / 1000.0, 1e-6));
        return new SimulationSettings
        {
            Analysis = analysis,
            StartTime = solverStart,
            StopTime = solverStop,
            StepTime = solverStep,
            TimeScalingEnabled = scaleEnabled,
            DeviceTimeScale = scaleEnabled ? scale : 1.0,
            SolverElapsedTimeSeconds = analysis == AnalysisType.Transient
                ? Math.Max(0, SimTransientStopTime - SimTransientStartTime)
                : 0,
            DeviceTimeSeconds = _deviceTimeSeconds > 0 ? _deviceTimeSeconds : solverStart * scale,
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

    private static double GetConfiguredDeviceTimeScale()
    {
        string simulationUnit = Preferences.Default.Get("Settings.SimulationTimeUnit", "seconds");
        string deviceUnit = Preferences.Default.Get("Settings.DeviceTimeUnit", "hours");
        double simulationAmount = Math.Max(Preferences.Default.Get("Settings.SimulationTimeAmount", 30.0), 1e-9);
        double deviceAmount = Math.Max(Preferences.Default.Get("Settings.DeviceTimeAmount", 1.0), 1e-9);
        return deviceAmount * SettingsViewModel.TimeUnitSeconds(deviceUnit) /
               (simulationAmount * SettingsViewModel.TimeUnitSeconds(simulationUnit));
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
        if (_playbackSolveRunning || IsSimulating)
        {
            _simulationRefreshRequested = true;
            return;
        }
        _simulationRefreshRequested = false;
        try
        {
            IsSimulating  = true;
            StatusText    = $"Simulating ({SelectedAnalysisType})…";
            ConsoleOutput = "";

            var settings = BuildSimulationSettings();
            var results  = await Task.Run(() => _simManager.Run(Circuit, settings));
            if (settings.Analysis == AnalysisType.Transient)
            {
                double scale = settings.TimeScalingEnabled ? settings.DeviceTimeScale : 1.0;
                _solverTimeSeconds = settings.StopTime;
                _deviceTimeSeconds = settings.DeviceTimeSeconds +
                    Math.Max(0, settings.StopTime - settings.StartTime) * scale;
            }
            _lastSimulationCircuitSnapshot = EeePlatformSerializer.ExportToEee(Circuit);
            DispatchTriggeredUiEvents();

            ConsoleOutput = FormatResults(results);
            AiSchemaStatus = results.Success
                ? "Simulation feedback refreshed for the next AI request."
                : $"Simulation feedback unavailable: {results.Message}";
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
            OnPropertyChanged(nameof(SelectedComponent));
            CircuitChanged?.Invoke();
            if (_simulationRefreshRequested)
            {
                _simulationRefreshRequested = false;
                if (IsSimulationPlaying)
                    _ = RefreshPlaybackSimulationAsync();
                else
                    _ = SimulateAsync();
            }
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

        if (item.Type.StartsWith("custom:", StringComparison.OrdinalIgnoreCase))
        {
            var definitionId = item.Type["custom:".Length..];
            if (!_customCircuitDefinitions.TryGetValue(definitionId, out var definition))
            {
                StatusText = "That saved custom component could not be loaded.";
                return;
            }
            var custom = new CustomCircuitComponent { Name = name, Reference = name, X = cx, Y = cy };
            custom.SetDefinition(definition);
            var customCommand = new AddComponentCommand(Circuit, custom) { Origin = CommandOrigin.Schematic };
            CommandManager.ExecuteCommand(customCommand);
            StatusText = $"Added custom component {name} ({definition.Name})";
            CircuitChanged?.Invoke();
            return;
        }

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
        SelectedConnection = null;
        SelectedComponent = comp;
        SelectedComponentEventTriggers.Clear();
        foreach (var trigger in comp.EventTriggers)
            SelectedComponentEventTriggers.Add(trigger);
        EditName = comp.Reference;
        EditType = comp.ComponentType;
        EditDescription = comp.CustomProperties.TryGetValue("Description", out var dVal) ? dVal?.ToString() ?? comp.Description : comp.Description;
        EditBreakingCurrent = comp.BreakingCurrent;

        var param = comp.ElectricalParameters.FirstOrDefault();
        EditParamName = param.Key ?? "Value";
        EditParamValue = param.Value;
        EditShowLiveProbe = comp.ShowLiveProbe;
        EditShowWaveform = comp.ShowWaveform;

        string paramStr = param.Key != null ? $"{param.Key}={param.Value:G4}" : "";
        ActiveComponentInfo = $"{comp.Reference}  [{comp.ComponentType}]  ({comp.X:F0}, {comp.Y:F0})  {paramStr}";
        StatusText = $"Selected {comp.Reference} [{comp.ComponentType}]";
    }

    public void OnConnectionSelected(ConnectionNode connection)
    {
        SelectedComponent = null;
        SelectedConnection = connection;
        ActiveComponentInfo = $"Wire {connection.NetName}: {connection.FromPin} → {connection.ToPin}";
        StatusText = $"Selected wire {connection.NetName}";
    }

    [RelayCommand]
    public void AddComponentEventTrigger()
    {
        if (SelectedComponent == null) return;
        var trigger = new SimulationEventTrigger
        {
            Name = $"Event {SelectedComponentEventTriggers.Count + 1}",
            Measurement = "Voltage",
            Comparison = ">=",
            Threshold = 1,
            Target = "Chat bubble",
            Message = "Voltage threshold reached",
            Mode = "Latched"
        };
        SelectedComponentEventTriggers.Add(trigger);
        SaveComponentEventTriggers();
    }

    [RelayCommand]
    public void RemoveComponentEventTrigger(SimulationEventTrigger trigger)
    {
        if (trigger != null && SelectedComponentEventTriggers.Remove(trigger))
            SaveComponentEventTriggers();
    }

    [RelayCommand]
    public void SaveComponentEventTriggers()
    {
        if (SelectedComponent == null) return;
        SelectedComponent.EventTriggers = SelectedComponentEventTriggers.ToList();
        SelectedComponent.PersistEventTriggers();
        CircuitChanged?.Invoke();
        StatusText = $"Saved {SelectedComponentEventTriggers.Count} UI event trigger(s) for {SelectedComponent.Reference}";
    }

    [RelayCommand]
    public void CloseSimulationEventDialog() => IsSimulationEventDialogVisible = false;

    [RelayCommand]
    public void DismissSimulationEventChat() => IsSimulationEventChatVisible = false;

    public void FireNamedUiEvent(string eventName, string? message = null)
    {
        var match = Circuit.Components.SelectMany(component => component.EventTriggers
                .Select(trigger => (Component: component, Trigger: trigger)))
            .FirstOrDefault(item => item.Trigger.Name.Equals(eventName, StringComparison.OrdinalIgnoreCase) ||
                                    item.Trigger.Id.Equals(eventName, StringComparison.OrdinalIgnoreCase));
        if (match.Trigger == null)
        {
            ShowSimulationEventChat(message ?? eventName);
            return;
        }

        match.Trigger.IsFired = true;
        DispatchUiEvent(match.Component, match.Trigger.Name, match.Trigger.Target, message ?? match.Trigger.Message, match.Trigger.Id);
    }

    private void ToggleNamedUiEvent(string eventName)
    {
        var match = Circuit.Components.SelectMany(component => component.EventTriggers
                .Select(trigger => (Component: component, Trigger: trigger)))
            .FirstOrDefault(item => item.Trigger.Name.Equals(eventName, StringComparison.OrdinalIgnoreCase) ||
                                    item.Trigger.Id.Equals(eventName, StringComparison.OrdinalIgnoreCase));
        if (match.Trigger == null)
        {
            FireNamedUiEvent(eventName);
            return;
        }

        if (!match.Trigger.IsFired)
        {
            FireNamedUiEvent(eventName);
            return;
        }

        match.Trigger.IsFired = false;
        if (_simulationEventOwnersByTarget.TryGetValue(match.Trigger.Target, out var owner) &&
            owner.Equals(match.Trigger.Id, StringComparison.OrdinalIgnoreCase))
        {
            _simulationEventOwnersByTarget.Remove(match.Trigger.Target);
            switch (match.Trigger.Target)
            {
                case "Dialog": IsSimulationEventDialogVisible = false; break;
                case "Waveform drawer": IsWaveformDrawerOpen = false; break;
                case "Description sidebar": IsDescriptionSidebarVisible = false; break;
                case "Control sidebar": IsControlSidebarVisible = false; break;
                case "Chat bubble": IsSimulationEventChatVisible = false; break;
            }
        }
        _activeSimulationEventIds.Remove(match.Trigger.Id);
        StatusText = $"UI event '{match.Trigger.Name}' toggled off";
    }

    private ComponentNode? FindComponentTarget(string target)
    {
        string componentName = target.Split(':', 2)[0].Trim();
        return Circuit.Components.FirstOrDefault(component =>
            component.Name.Equals(componentName, StringComparison.OrdinalIgnoreCase) ||
            component.Reference.Equals(componentName, StringComparison.OrdinalIgnoreCase));
    }

    private void SetWaveformTarget(string target, bool enabled)
    {
        var component = FindComponentTarget(target);
        if (component == null)
            throw new InvalidOperationException($"Component or node '{target}' was not found.");

        string[] targetParts = target.Split(':', 2);
        if (targetParts.Length == 2 && component.GetPin(targetParts[1]) == null)
            throw new InvalidOperationException($"Node '{targetParts[1]}' does not exist on {component.Reference}.");

        if (enabled)
        {
            ConfigureWaveformForComponent(component, showTerminalPicker: false);
            if (targetParts.Length == 2)
            {
                var pin = component.GetPin(targetParts[1]);
                WaveformSignals.Clear();
                WaveformSignals.Add(new WaveformSignal(component, pin!.Id,
                    $"{component.Reference}:{pin.Name} voltage", WaveformSignalKind.PinVoltage));
            }
        }
        else
        {
            PythonApi.SetWaveform(component.Name, false);
            if (WaveformComponent == component)
                IsWaveformDrawerOpen = false;
        }
    }

    private void DispatchTriggeredUiEvents()
    {
        var activeEvents = ComponentEventEvaluator.Evaluate(Circuit);
        var activeIds = activeEvents.Select(item => item.TriggerId).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var item in activeEvents)
        {
            if (!_activeSimulationEventIds.Add(item.TriggerId)) continue;
            var component = Circuit.Components.FirstOrDefault(candidate => candidate.Id == item.ComponentId);
            if (component != null)
                DispatchUiEvent(component, item.Name, item.Target, item.Message, item.TriggerId);
        }

        foreach (var inactiveId in _activeSimulationEventIds.Where(id => !activeIds.Contains(id)).ToList())
        {
            _activeSimulationEventIds.Remove(inactiveId);
            foreach (var target in _simulationEventOwnersByTarget.Where(pair => pair.Value == inactiveId).Select(pair => pair.Key).ToList())
            {
                _simulationEventOwnersByTarget.Remove(target);
                if (target == "Dialog") IsSimulationEventDialogVisible = false;
                if (target == "Chat bubble") IsSimulationEventChatVisible = false;
            }
        }
    }

    private void DispatchUiEvent(ComponentNode source, string eventName, string target, string message, string triggerId)
    {
        _simulationEventOwnersByTarget[target] = triggerId;
        switch (target)
        {
            case "Dialog":
                SimulationEventDialogTitle = $"{source.Reference}: {eventName}";
                SimulationEventDialogMessage = message;
                IsSimulationEventDialogVisible = true;
                break;
            case "Waveform drawer":
                var targetComponent = WaveformComponent ?? SelectedComponent;
                if (targetComponent != null)
                    ConfigureWaveformForComponent(targetComponent, showTerminalPicker: false);
                else
                    IsWaveformDrawerOpen = true;
                break;
            case "Description sidebar":
                IsDescriptionSidebarVisible = true;
                break;
            case "Control sidebar":
                IsControlSidebarVisible = true;
                IsSidebarVisible = false;
                break;
            default:
                ShowSimulationEventChat(message);
                break;
        }
        StatusText = $"UI event '{eventName}' fired by {source.Reference}";
    }

    private void ShowSimulationEventChat(string message)
    {
        SimulationEventChatMessage = message;
        IsSimulationEventChatVisible = true;
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
        RefreshPowerSystemProperties(comp);
        EditName = comp.Reference;
        EditType = comp.ComponentType;
        EditShowLiveProbe = comp.ShowLiveProbe;
        EditShowWaveform = comp.ShowWaveform;
        EditDescription = comp.CustomProperties.TryGetValue("Description", out var dVal) ? dVal?.ToString() ?? comp.Description : comp.Description;
        EditBreakingCurrent = comp.BreakingCurrent;
        SelectedEditTab = "General";

        // Reset component type flags
        IsSourceComponent = comp is VoltageSourceComponent || comp is CurrentSourceComponent;
        IsBjtComponent = comp is EESimulator.Core.Components.Electronics.BJTComponent;
        IsJfetComponent = comp is EESimulator.Core.Components.Electronics.JFETComponent;
        IsMosfetComponent = comp is EESimulator.Core.Components.Electronics.MOSFETComponent;
        IsDiodeComponent = comp is DiodeComponent;
        IsOpAmpComponent = comp is EESimulator.Core.Components.Electronics.OpAmpComponent;
        IsMutualInductanceComponent = comp is MutualInductanceComponent;
        IsResistorComponent = comp is ResistorComponent;
        IsCapacitorComponent = comp is CapacitorComponent;
        IsInductorComponent = comp is InductorComponent;
        IsWireComponent = comp is WireComponent;
        IsSwitchComponent = comp is VoltageSwitchComponent || comp is CurrentSwitchComponent;
        IsControlledSourceComponent = comp is VCVSComponent || comp is VCCSComponent || comp is CCVSComponent || comp is CCCSComponent;
        IsTransmissionLineComponent = comp is TransmissionLineComponent;
        IsVoltageDelayComponent = comp is VoltageDelayComponent;
        IsClockComponent = comp is EESimulator.Core.Components.Electronics.ClockComponent;
        IsPinComponent = comp is EESimulator.Core.Components.Electronics.PinComponent;
        IsPowerComponent = comp is EESimulator.Core.Components.Electrical.PowerComponent;
        IsPullResistorComponent = comp is EESimulator.Core.Components.Electrical.PullResistorComponent;
        IsTunnelComponent = comp is EESimulator.Core.Components.General.TunnelComponent;
        IsProbeComponent = comp is EESimulator.Core.Components.General.ProbeComponent;
        IsDigitalTransistorComponent = comp is EESimulator.Core.Components.Electronics.DigitalTransistorComponent;
        IsTransmissionGateComponent = comp is EESimulator.Core.Components.Electronics.TransmissionGateComponent;
        IsBitExtenderComponent = comp is EESimulator.Core.Components.Electronics.BitExtenderComponent;
        IsConstantComponent = comp is EESimulator.Core.Components.Electronics.ConstantComponent;
        IsDoNotConnectComponent = comp is EESimulator.Core.Components.General.DoNotConnectComponent;
        IsTtlPackageComponent = comp is TtlPackageComponent;
        IsLossyTransmissionLineComponent = comp is EESimulator.Core.Components.Electrical.LossyTransmissionLineComponent;
        IsCoaxialCableComponent = comp is EESimulator.Core.Components.Electrical.CoaxialCableComponent;
        IsMicrostripLineComponent = comp is EESimulator.Core.Components.Electrical.MicrostripLineComponent;

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
        else if (comp is EESimulator.Core.Components.Electronics.JFETComponent jfet)
        {
            EditParamName = "Pinch-off Voltage Vto (V)";
            EditParamValue = jfet.ThresholdVoltage;
            EditThresholdVoltage = jfet.ThresholdVoltage;
            EditTransconductance = jfet.Transconductance;
            EditLambda = jfet.Lambda;
            EditJfetChannel = jfet.Channel == EESimulator.Core.Components.Electronics.JfetChannel.PChannel ? "PChannel" : "NChannel";
        }
        else if (comp is VCVSComponent vcvs)
        {
            EditParamName = "Voltage Gain (μ)";
            EditParamValue = vcvs.VoltageGain;
        }
        else if (comp is VCCSComponent vccs)
        {
            EditParamName = "Transconductance Gm (A/V)";
            EditParamValue = vccs.Transconductance;
        }
        else if (comp is CCVSComponent ccvs)
        {
            EditParamName = "Transresistance Rm (V/A)";
            EditParamValue = ccvs.Transresistance;
            EditControllingSource = ccvs.ControllingSource;
        }
        else if (comp is CCCSComponent cccs)
        {
            EditParamName = "Current Gain (β)";
            EditParamValue = cccs.CurrentGain;
            EditControllingSource = cccs.ControllingSource;
        }
        else if (comp is VoltageSwitchComponent vsw)
        {
            EditParamName = "ON Resistance Ron (Ω)";
            EditParamValue = vsw.ResistanceOn;
            EditResistanceOff = vsw.ResistanceOff;
            EditThreshold = vsw.Threshold;
            EditHysteresis = vsw.Hysteresis;
        }
        else if (comp is CurrentSwitchComponent csw)
        {
            EditParamName = "ON Resistance Ron (Ω)";
            EditParamValue = csw.ResistanceOn;
            EditResistanceOff = csw.ResistanceOff;
            EditThreshold = csw.Threshold;
            EditHysteresis = csw.Hysteresis;
            EditControllingSource = csw.ControllingSource;
        }
        else if (comp is TransmissionLineComponent tline)
        {
            EditParamName = "Impedance Z0 (Ω)";
            EditParamValue = tline.CharacteristicImpedance;
            EditCharacteristicImpedance = tline.CharacteristicImpedance;
            EditDelayTime = tline.Delay;
            EditLineFrequency = tline.Frequency;
            EditNormalizedLength = tline.NormalizedLength;
        }
        else if (comp is VoltageDelayComponent vdelay)
        {
            EditParamName = "Delay Time (s)";
            EditParamValue = vdelay.Delay;
            EditDelayTime = vdelay.Delay;
        }
        else if (comp is EESimulator.Core.Components.Electronics.ClockComponent clk)
        {
            EditParamName = "Frequency (Hz)";
            EditParamValue = clk.Frequency;
            EditClockFrequency = clk.Frequency;
            EditClockDutyCycle = clk.DutyCycle;
            EditClockHighVoltage = clk.HighVoltage;
            EditClockLowVoltage = clk.LowVoltage;
            EditClockHighDuration = clk.HighDuration;
            EditClockLowDuration = clk.LowDuration;
            EditClockPhase = clk.Phase;
        }
        else if (comp is EESimulator.Core.Components.Electronics.PinComponent pin)
        {
            EditParamName = "Value / Logic Level";
            EditParamValue = pin.Value;
            EditPinDirection = pin.Direction.ToString();
            EditPinBitWidth = pin.BitWidth;
            EditPinBehavior = pin.Behavior.ToString();
            EditPinInitialValue = pin.InitialValue;
            EditPinRadix = pin.Radix.ToString();
            EditPinHighVoltage = pin.HighVoltage;
            EditPinLowVoltage = pin.LowVoltage;
        }
        else if (comp is EESimulator.Core.Components.Electrical.PowerComponent pw)
        {
            EditParamName = "Rail Voltage (V)";
            EditParamValue = pw.Voltage;
            EditPowerVoltage = pw.Voltage;
        }
        else if (comp is EESimulator.Core.Components.Electrical.PullResistorComponent pr)
        {
            EditParamName = "Resistance (Ω)";
            EditParamValue = pr.Resistance;
            EditPullMode = pr.Mode.ToString();
            EditPullResistance = pr.Resistance;
            EditPullRailVoltage = pr.RailVoltage;
        }
        else if (comp is EESimulator.Core.Components.General.TunnelComponent tun)
        {
            EditParamName = "Tunnel Label";
            EditTunnelLabel = tun.TunnelLabel;
            EditTunnelBitWidth = tun.BitWidth;
        }
        else if (comp is EESimulator.Core.Components.General.ProbeComponent prb)
        {
            EditParamName = "Input Impedance (Ω)";
            EditParamValue = prb.InputImpedance;
            EditProbeRadix = prb.Radix.ToString();
            EditProbeInputImpedance = prb.InputImpedance;
        }
        else if (comp is EESimulator.Core.Components.Electronics.DigitalTransistorComponent dtr)
        {
            EditParamName = "On Resistance Ron (Ω)";
            EditParamValue = dtr.ResistanceOn;
            EditTransistorType = dtr.TransistorType.ToString();
            EditTransistorGateLocation = dtr.GateLocation.ToString();
            EditTransistorBitWidth = dtr.BitWidth;
            EditTransistorRon = dtr.ResistanceOn;
            EditTransistorRoff = dtr.ResistanceOff;
            EditTransistorThreshold = dtr.Threshold;
        }
        else if (comp is EESimulator.Core.Components.Electronics.TransmissionGateComponent tg)
        {
            EditParamName = "On Resistance Ron (Ω)";
            EditParamValue = tg.ResistanceOn;
            EditTransmissionGateSelectLoc = tg.SelectLocation.ToString();
            EditTransmissionGateBitWidth = tg.BitWidth;
            EditTransmissionGateRon = tg.ResistanceOn;
            EditTransmissionGateRoff = tg.ResistanceOff;
        }
        else if (comp is EESimulator.Core.Components.Electronics.BitExtenderComponent be)
        {
            EditParamName = "Output Bit Width";
            EditParamValue = be.OutputBitWidth;
            EditBitExtenderInWidth = be.InputBitWidth;
            EditBitExtenderOutWidth = be.OutputBitWidth;
            EditBitExtenderType = be.ExtensionType.ToString();
        }
        else if (comp is EESimulator.Core.Components.Electronics.ConstantComponent cst)
        {
            EditParamName = "Constant Value";
            EditParamValue = cst.Value;
            EditConstantValue = cst.Value;
            EditConstantBitWidth = cst.BitWidth;
            EditConstantVoltageHigh = cst.VoltageHigh;
            EditConstantVoltageLow = cst.VoltageLow;
        }
        else if (comp is TtlPackageComponent ttl)
        {
            EditParamName = "DIP Pin Count";
            EditParamValue = ttl.PackagePinCount;
            EditTtlExposeSupplyPins = ttl.ExposeSupplyPins;
            EditTtlShowInternalStructure = ttl.ShowInternalStructure;
        }
        else if (comp is EESimulator.Core.Components.General.DoNotConnectComponent dnc)
        {
            EditParamName = "Bit Width";
            EditParamValue = dnc.BitWidth;
            EditDncBitWidth = dnc.BitWidth;
        }
        else if (comp is EESimulator.Core.Components.Electrical.LossyTransmissionLineComponent ltl)
        {
            EditParamName = "Length (m)";
            EditParamValue = ltl.Length;
            EditLossyResistance = ltl.ResistancePerMeter;
            EditLossyInductance = ltl.InductancePerMeter;
            EditLossyConductance = ltl.ConductancePerMeter;
            EditLossyCapacitance = ltl.CapacitancePerMeter;
            EditLossyLength = ltl.Length;
        }
        else if (comp is EESimulator.Core.Components.Electrical.CoaxialCableComponent coax)
        {
            EditParamName = "Impedance Z0 (Ω)";
            EditParamValue = coax.CharacteristicImpedance;
            EditCoaxZ0 = coax.CharacteristicImpedance;
            EditCoaxVF = coax.VelocityFactor;
            EditCoaxLength = coax.Length;
            EditCoaxAttenuation = coax.Attenuation;
        }
        else if (comp is EESimulator.Core.Components.Electrical.MicrostripLineComponent ustrip)
        {
            EditParamName = "Trace Width (m)";
            EditParamValue = ustrip.TraceWidth;
            EditMicrostripWidth = ustrip.TraceWidth;
            EditMicrostripHeight = ustrip.SubstrateHeight;
            EditMicrostripEr = ustrip.DielectricConstant;
            EditMicrostripLength = ustrip.Length;
        }
        else if (comp is WireComponent w)
        {
            EditParamName = "Parasitic Resistance (Ω)";
            EditParamValue = w.Resistance;
            EditWireResistance = w.Resistance.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }
        else if (comp is EESimulator.Core.Components.Electrical.BulbComponent bulb)
        {
            EditParamName = "Filament Resistance (Ω)";
            EditParamValue = bulb.Resistance;
        }
        else if (comp is EESimulator.Core.Components.Electronics.LEDComponent led)
        {
            EditParamName = "Forward Voltage Vf (V)";
            EditParamValue = led.ForwardVoltage;
        }
        else if (comp is EESimulator.Core.Components.Electronics.LogicInputComponent lin)
        {
            EditParamName = "Logic 1 High Voltage (V)";
            EditParamValue = lin.HighVoltage;
        }
        else if (comp is EESimulator.Core.Components.Electronics.LogicOutputComponent lout)
        {
            EditParamName = "Logic Switching Threshold (V)";
            EditParamValue = lout.Threshold;
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

    private void RefreshPowerSystemProperties(ComponentNode component)
    {
        PowerSystemProperties.Clear();
        if (component is not PowerSystemComponent) return;

        var type = component.GetType();
        var properties = type.GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Where(property => property.GetIndexParameters().Length == 0 && property.GetMethod?.IsPublic == true &&
                               property.Name is not ("ComponentType" or "StableTypeId" or "SpicePrefix" or "Category" or "ModelName" or "SymbolKey" or "IsSimulatable" or
                                   "Description" or "Pins" or "Children" or "Parent" or "ElectricalParameters" or "CustomProperties" or "EventTriggers" or
                                   "WaveformTime" or "WaveformVoltage" or "WaveformCurrent" or "WaveformNodeVoltages" or "Position" or "Id" or "Name" or "Reference" or
                                   "NodeType" or "Tags" or "UserData" or "X" or "Y" or "Rotation" or "ScaleX" or "ScaleY" or "Enabled" or "Visible" or
                                   "Locked" or "Selected" or "ShowLiveProbe" or "ShowWaveform" or "LiveVoltage" or "LiveCurrent" or "BreakingCurrent" or "IsBrokenDown"))
            .OrderBy(property => property.SetMethod?.IsPublic == true ? 0 : 1)
            .ThenBy(property => property.Name, StringComparer.OrdinalIgnoreCase);

        foreach (var property in properties)
        {
            try
            {
                object? value = property.GetValue(component);
                var item = new PowerSystemPropertyItem
                {
                    Name = property.Name,
                    TypeName = property.PropertyType.Name,
                    Value = Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty,
                    IsEditable = property.SetMethod?.IsPublic == true && property.PropertyType.IsPrimitive ||
                                 property.SetMethod?.IsPublic == true && property.PropertyType == typeof(string)
                };
                item.ReturnCommand = new RelayCommand(() => ApplyPowerSystemProperty(item));
                PowerSystemProperties.Add(item);
            }
            catch
            {
                // Skip a property that cannot currently be evaluated for the inspector.
            }
        }
    }

    [RelayCommand]
    public void ApplyPowerSystemProperty(PowerSystemPropertyItem item)
    {
        if (EditingComponent is not PowerSystemComponent component || item == null || !item.IsEditable) return;
        var property = component.GetType().GetProperty(item.Name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase);
        if (property?.SetMethod?.IsPublic != true) return;
        try
        {
            object converted = property.PropertyType == typeof(string)
                ? item.Value
                : Convert.ChangeType(item.Value, property.PropertyType, System.Globalization.CultureInfo.InvariantCulture);
            property.SetValue(component, converted);
            component.ValidateConfiguration();
            component.NotifyModified(item.Name);
            RefreshPowerSystemProperties(component);
            CircuitChanged?.Invoke();
            RequestSimulationRefresh();
            StatusText = $"Updated {component.Reference}.{item.Name}";
        }
        catch (Exception ex)
        {
            StatusText = $"Invalid {item.Name}: {ex.Message}";
            RefreshPowerSystemProperties(component);
        }
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
                ConfigureWaveformForComponent(EditingComponent, showTerminalPicker: false);
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

        EditingComponent.CustomProperties["Description"] = EditDescription;
        EditingComponent.BreakingCurrent = Math.Max(EditBreakingCurrent, 0.0);

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
        else if (EditingComponent is EESimulator.Core.Components.Electronics.JFETComponent jfet)
        {
            jfet.ThresholdVoltage = EditThresholdVoltage != 0 ? EditThresholdVoltage : EditParamValue;
            jfet.Transconductance = Math.Max(EditTransconductance, 1e-9);
            jfet.Lambda = EditLambda;
            jfet.Channel = EditJfetChannel.Equals("PChannel", StringComparison.OrdinalIgnoreCase)
                ? EESimulator.Core.Components.Electronics.JfetChannel.PChannel
                : EESimulator.Core.Components.Electronics.JfetChannel.NChannel;
        }
        else if (EditingComponent is VCVSComponent vcvs)
        {
            vcvs.VoltageGain = EditParamValue;
        }
        else if (EditingComponent is VCCSComponent vccs)
        {
            vccs.Transconductance = EditParamValue;
        }
        else if (EditingComponent is CCVSComponent ccvs)
        {
            ccvs.Transresistance = EditParamValue;
            ccvs.ControllingSource = EditControllingSource;
        }
        else if (EditingComponent is CCCSComponent cccs)
        {
            cccs.CurrentGain = EditParamValue;
            cccs.ControllingSource = EditControllingSource;
        }
        else if (EditingComponent is VoltageSwitchComponent vsw)
        {
            vsw.ResistanceOn = Math.Max(EditParamValue, 1e-9);
            vsw.ResistanceOff = Math.Max(EditResistanceOff, 1e-3);
            vsw.Threshold = EditThreshold;
            vsw.Hysteresis = EditHysteresis;
        }
        else if (EditingComponent is CurrentSwitchComponent csw)
        {
            csw.ResistanceOn = Math.Max(EditParamValue, 1e-9);
            csw.ResistanceOff = Math.Max(EditResistanceOff, 1e-3);
            csw.Threshold = EditThreshold;
            csw.Hysteresis = EditHysteresis;
            csw.ControllingSource = EditControllingSource;
        }
        else if (EditingComponent is TransmissionLineComponent tline)
        {
            tline.CharacteristicImpedance = EditCharacteristicImpedance > 0 ? EditCharacteristicImpedance : Math.Max(EditParamValue, 1e-3);
            tline.Delay = Math.Max(EditDelayTime, 1e-15);
            tline.Frequency = Math.Max(EditLineFrequency, 1.0);
            tline.NormalizedLength = Math.Max(EditNormalizedLength, 0.0);
        }
        else if (EditingComponent is VoltageDelayComponent vdelay)
        {
            vdelay.Delay = Math.Max(EditParamValue > 0 ? EditParamValue : EditDelayTime, 1e-15);
        }
        else if (EditingComponent is WireComponent w)
        {
            w.Resistance = double.TryParse(EditWireResistance, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var wr) ? wr : Math.Max(EditParamValue, 0);
        }
        else if (EditingComponent is EESimulator.Core.Components.Electrical.BulbComponent bulb)
        {
            bulb.Resistance = Math.Max(EditParamValue, 1e-6);
        }
        else if (EditingComponent is EESimulator.Core.Components.Electronics.LEDComponent led)
        {
            led.ForwardVoltage = Math.Max(EditParamValue, 0.1);
        }
        else if (EditingComponent is EESimulator.Core.Components.Electronics.LogicInputComponent lin)
        {
            lin.HighVoltage = EditParamValue;
        }
        else if (EditingComponent is EESimulator.Core.Components.Electronics.LogicOutputComponent lout)
        {
            lout.Threshold = EditParamValue;
        }
        else if (EditingComponent is EESimulator.Core.Components.Electronics.ClockComponent clk)
        {
            clk.Frequency = EditClockFrequency > 0 ? EditClockFrequency : Math.Max(EditParamValue, 0.01);
            clk.DutyCycle = Math.Clamp(EditClockDutyCycle, 0.01, 0.99);
            clk.HighVoltage = EditClockHighVoltage;
            clk.LowVoltage = EditClockLowVoltage;
            clk.HighDuration = Math.Max(EditClockHighDuration, 1);
            clk.LowDuration = Math.Max(EditClockLowDuration, 1);
            clk.Phase = Math.Max(EditClockPhase, 0);
        }
        else if (EditingComponent is EESimulator.Core.Components.Electronics.PinComponent pin)
        {
            pin.Value = (long)EditParamValue;
            if (Enum.TryParse<EESimulator.Core.Components.Electronics.PinDirection>(EditPinDirection, true, out var pdir))
                pin.Direction = pdir;
            pin.BitWidth = Math.Clamp(EditPinBitWidth, 1, 32);
            if (Enum.TryParse<EESimulator.Core.Components.Electronics.PinBehavior>(EditPinBehavior, true, out var pbeh))
                pin.Behavior = pbeh;
            pin.InitialValue = EditPinInitialValue;
            if (Enum.TryParse<EESimulator.Core.Components.Electronics.DisplayRadix>(EditPinRadix, true, out var prad))
                pin.Radix = prad;
            pin.HighVoltage = EditPinHighVoltage;
            pin.LowVoltage = EditPinLowVoltage;
        }
        else if (EditingComponent is EESimulator.Core.Components.Electrical.PowerComponent pw)
        {
            pw.Voltage = EditPowerVoltage != 0 ? EditPowerVoltage : EditParamValue;
        }
        else if (EditingComponent is EESimulator.Core.Components.Electrical.PullResistorComponent pr)
        {
            pr.Resistance = EditPullResistance > 0 ? EditPullResistance : Math.Max(EditParamValue, 1.0);
            pr.RailVoltage = EditPullRailVoltage;
            if (Enum.TryParse<EESimulator.Core.Components.Electrical.PullResistorMode>(EditPullMode, true, out var prm))
                pr.Mode = prm;
        }
        else if (EditingComponent is EESimulator.Core.Components.General.TunnelComponent tun)
        {
            if (!string.IsNullOrWhiteSpace(EditTunnelLabel))
                tun.TunnelLabel = EditTunnelLabel.Trim();
            tun.BitWidth = Math.Clamp(EditTunnelBitWidth, 1, 32);
        }
        else if (EditingComponent is EESimulator.Core.Components.General.ProbeComponent prb)
        {
            prb.InputImpedance = EditProbeInputImpedance > 0 ? EditProbeInputImpedance : Math.Max(EditParamValue, 1e3);
            if (Enum.TryParse<EESimulator.Core.Components.Electronics.DisplayRadix>(EditProbeRadix, true, out var prad))
                prb.Radix = prad;
        }
        else if (EditingComponent is EESimulator.Core.Components.Electronics.DigitalTransistorComponent dtr)
        {
            if (Enum.TryParse<EESimulator.Core.Components.Electronics.DigitalTransistorType>(EditTransistorType, true, out var dtt))
                dtr.TransistorType = dtt;
            if (Enum.TryParse<EESimulator.Core.Components.Electronics.GateLocation>(EditTransistorGateLocation, true, out var gloc))
                dtr.GateLocation = gloc;
            dtr.BitWidth = Math.Clamp(EditTransistorBitWidth, 1, 32);
            dtr.ResistanceOn = EditTransistorRon > 0 ? EditTransistorRon : Math.Max(EditParamValue, 1e-4);
            dtr.ResistanceOff = Math.Max(EditTransistorRoff, 100.0);
            dtr.Threshold = EditTransistorThreshold;
        }
        else if (EditingComponent is EESimulator.Core.Components.Electronics.TransmissionGateComponent tg)
        {
            if (Enum.TryParse<EESimulator.Core.Components.Electronics.GateLocation>(EditTransmissionGateSelectLoc, true, out var gloc))
                tg.SelectLocation = gloc;
            tg.BitWidth = Math.Clamp(EditTransmissionGateBitWidth, 1, 32);
            tg.ResistanceOn = EditTransmissionGateRon > 0 ? EditTransmissionGateRon : Math.Max(EditParamValue, 1e-4);
            tg.ResistanceOff = Math.Max(EditTransmissionGateRoff, 100.0);
        }
        else if (EditingComponent is EESimulator.Core.Components.Electronics.BitExtenderComponent be)
        {
            be.InputBitWidth = Math.Clamp(EditBitExtenderInWidth, 1, 32);
            be.OutputBitWidth = Math.Clamp(EditBitExtenderOutWidth, 1, 64);
            if (Enum.TryParse<EESimulator.Core.Components.Electronics.BitExtensionType>(EditBitExtenderType, true, out var bet))
                be.ExtensionType = bet;
        }
        else if (EditingComponent is EESimulator.Core.Components.Electronics.ConstantComponent cst)
        {
            cst.Value = EditConstantValue != 0 ? EditConstantValue : (long)EditParamValue;
            cst.BitWidth = Math.Clamp(EditConstantBitWidth, 1, 32);
            cst.VoltageHigh = EditConstantVoltageHigh;
            cst.VoltageLow = EditConstantVoltageLow;
        }
        else if (EditingComponent is TtlPackageComponent ttl)
        {
            ttl.ExposeSupplyPins = EditTtlExposeSupplyPins;
            ttl.ShowInternalStructure = EditTtlShowInternalStructure;
        }
        else if (EditingComponent is EESimulator.Core.Components.General.DoNotConnectComponent dnc)
        {
            dnc.BitWidth = Math.Clamp(EditDncBitWidth > 0 ? EditDncBitWidth : (int)EditParamValue, 1, 32);
        }
        else if (EditingComponent is EESimulator.Core.Components.Electrical.LossyTransmissionLineComponent ltl)
        {
            ltl.Length = EditLossyLength > 0 ? EditLossyLength : Math.Max(EditParamValue, 0.001);
            ltl.ResistancePerMeter = Math.Max(EditLossyResistance, 0.0);
            ltl.InductancePerMeter = Math.Max(EditLossyInductance, 1e-12);
            ltl.ConductancePerMeter = Math.Max(EditLossyConductance, 0.0);
            ltl.CapacitancePerMeter = Math.Max(EditLossyCapacitance, 1e-15);
        }
        else if (EditingComponent is EESimulator.Core.Components.Electrical.CoaxialCableComponent coax)
        {
            coax.CharacteristicImpedance = EditCoaxZ0 > 0 ? EditCoaxZ0 : Math.Max(EditParamValue, 1.0);
            coax.VelocityFactor = Math.Clamp(EditCoaxVF, 0.1, 1.0);
            coax.Length = Math.Max(EditCoaxLength, 0.01);
            coax.Attenuation = Math.Max(EditCoaxAttenuation, 0.0);
        }
        else if (EditingComponent is EESimulator.Core.Components.Electrical.MicrostripLineComponent ustrip)
        {
            ustrip.TraceWidth = EditMicrostripWidth > 0 ? EditMicrostripWidth : Math.Max(EditParamValue, 1e-6);
            ustrip.SubstrateHeight = Math.Max(EditMicrostripHeight, 1e-6);
            ustrip.DielectricConstant = Math.Max(EditMicrostripEr, 1.0);
            ustrip.Length = Math.Max(EditMicrostripLength, 1e-4);
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
            ConfigureWaveformForComponent(EditingComponent, showTerminalPicker: false);
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
            "Bulb" or "Lamp" => "LAMP",
            "LogicInput"     => "IN",
            "LogicOutput"    => "OUT",
            "BJT" or "NPN" or "PNP" => "Q",
            "JFET"           => "J",
            "MOSFET" or "NMOS" or "PMOS" => "M",
            "OpAmp"          => "U",
            "VCVS"           => "E",
            "VCCS"           => "G",
            "CCVS"           => "H",
            "CCCS"           => "F",
            "VoltageSwitch"  => "S",
            "CurrentSwitch"  => "W",
            "TransmissionLine" => "T",
            "VoltageDelay"   => "VD",
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
        // Strip variable assignment if present: e.g. "TL1 = circuit.add_..." or "v1 = eesim.add_..."
        int eqIdx = line.IndexOf('=');
        if (eqIdx > 0 && !line[..eqIdx].Contains('(') && !line[..eqIdx].Contains('"') && !line[..eqIdx].Contains('\''))
        {
            line = line[(eqIdx + 1)..].Trim();
        }

        string call;
        if (line.StartsWith("eesim.", StringComparison.OrdinalIgnoreCase))
            call = line["eesim.".Length..];
        else if (line.StartsWith("circuit.", StringComparison.OrdinalIgnoreCase))
            call = line["circuit.".Length..];
        else
            return $"[unknown] {line}";

        var parenIdx = call.IndexOf('(');
        if (parenIdx < 0) return "[error] malformed call";

        var method = call[..parenIdx].Trim();
        var lastParen = call.LastIndexOf(')');
        var argsStr = lastParen > parenIdx ? call[(parenIdx + 1)..lastParen].Trim() : string.Empty;
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

                case "add_ttl" when args.Length >= 4:
                    PythonApi.AddTtlPackage(Unquote(args[0]), Unquote(args[1]), ParseD(args[2]), ParseD(args[3]));
                    return $"[ok] TTL {Unquote(args[0])} package {Unquote(args[1])} added";

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

                case "set_component_property" when args.Length >= 3:
                    PythonApi.SetComponentProperty(Unquote(args[0]), Unquote(args[1]), ParseScriptValue(args[2]));
                    CircuitChanged?.Invoke();
                    return "[ok] component property set";

                case "set_custom_property" when args.Length >= 3:
                    PythonApi.SetCustomProperty(Unquote(args[0]), Unquote(args[1]), ParseScriptValue(args[2]));
                    CircuitChanged?.Invoke();
                    return "[ok] custom property set";

                case "set_memory_word" when args.Length >= 3:
                    PythonApi.SetMemoryWord(Unquote(args[0]), (int)ParseD(args[1]), (long)ParseD(args[2]));
                    CircuitChanged?.Invoke();
                    return "[ok] memory word set";

                case "set_live_probe" or "set_probe" when args.Length >= 2:
                    PythonApi.SetLiveProbe(Unquote(args[0]), bool.TryParse(Unquote(args[1]), out var lp) && lp);
                    CircuitChanged?.Invoke();
                    return $"[ok] live probe on {Unquote(args[0])} set to {args[1]}";

                case "set_waveform" when args.Length >= 2:
                    SetWaveformTarget(Unquote(args[0]), bool.TryParse(Unquote(args[1]), out var sw) && sw);
                    CircuitChanged?.Invoke();
                    return $"[ok] waveform for {Unquote(args[0])} set to {args[1]}";

                case "toggle_waveform" when args.Length >= 1:
                    string waveformTarget = Unquote(args[0]);
                    var waveformComponent = FindComponentTarget(waveformTarget);
                    if (waveformComponent == null) return $"[error] component or node '{waveformTarget}' not found";
                    SetWaveformTarget(waveformTarget, !waveformComponent.ShowWaveform);
                    CircuitChanged?.Invoke();
                    return $"[ok] waveform for {waveformTarget} toggled";

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
                    DispatchTriggeredUiEvents();
                    return FormatResults(r);

                case "fire_event" or "ui.fire_event" when args.Length >= 1:
                    string firedEventName = Unquote(args[0]);
                    string? firedEventMessage = args.Length > 1 ? Unquote(args[1]) : null;
                    FireNamedUiEvent(firedEventName, firedEventMessage);
                    return $"[ok] UI event '{firedEventName}' fired";

                case "toggle_event" or "ui.toggle_event" when args.Length >= 1:
                    ToggleNamedUiEvent(Unquote(args[0]));
                    return $"[ok] UI event '{Unquote(args[0])}' toggled";

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

                case "add_bulb" when args.Length >= 3:
                    PythonApi.AddBulb(Unquote(args[0]), ParseD(args[1]), ParseD(args[2]),
                        args.Length > 3 ? ParseD(args[3]) : 50.0,
                        args.Length > 4 ? ParseD(args[4]) : 6.0);
                    return $"[ok] bulb {Unquote(args[0])} added";

                case "add_led" when args.Length >= 3:
                    PythonApi.AddLED(Unquote(args[0]), ParseD(args[1]), ParseD(args[2]),
                        args.Length > 3 ? Unquote(args[3]) : "Red",
                        args.Length > 4 ? ParseD(args[4]) : 2.0);
                    return $"[ok] led {Unquote(args[0])} added";

                case "add_logic_input" when args.Length >= 3:
                    PythonApi.AddLogicInput(Unquote(args[0]), ParseD(args[1]), ParseD(args[2]),
                        args.Length > 3 && (bool.TryParse(Unquote(args[3]), out var bIni) ? bIni : ParseD(args[3]) > 0.5),
                        args.Length > 4 ? ParseD(args[4]) : 5.0,
                        args.Length > 5 ? ParseD(args[5]) : 0.0);
                    return $"[ok] logic input {Unquote(args[0])} added";

                case "add_logic_output" when args.Length >= 3:
                    PythonApi.AddLogicOutput(Unquote(args[0]), ParseD(args[1]), ParseD(args[2]),
                        args.Length > 3 ? ParseD(args[3]) : 2.5);
                    return $"[ok] logic output {Unquote(args[0])} added";

                case "add_clock" when args.Length >= 3:
                    PythonApi.AddClock(Unquote(args[0]), ParseD(args[1]), ParseD(args[2]),
                        args.Length > 3 ? ParseD(args[3]) : 1000.0,
                        args.Length > 4 ? ParseD(args[4]) : 0.5,
                        args.Length > 5 ? ParseD(args[5]) : 5.0,
                        args.Length > 6 ? ParseD(args[6]) : 0.0);
                    return $"[ok] clock {Unquote(args[0])} added";

                case "add_pin" when args.Length >= 3:
                    PythonApi.AddPin(Unquote(args[0]), ParseD(args[1]), ParseD(args[2]),
                        args.Length > 3 ? Unquote(args[3]) : "Input",
                        args.Length > 4 ? (int)ParseD(args[4]) : 1,
                        args.Length > 5 ? (long)ParseD(args[5]) : 0L);
                    return $"[ok] pin {Unquote(args[0])} added";

                case "add_power" when args.Length >= 3:
                    PythonApi.AddPower(Unquote(args[0]), ParseD(args[1]), ParseD(args[2]),
                        args.Length > 3 ? ParseD(args[3]) : 5.0);
                    return $"[ok] power rail {Unquote(args[0])} added";

                case "add_pull_resistor" or "add_pullresistor" when args.Length >= 3:
                    PythonApi.AddPullResistor(Unquote(args[0]), ParseD(args[1]), ParseD(args[2]),
                        args.Length > 3 ? Unquote(args[3]) : "PullUp",
                        args.Length > 4 ? ParseD(args[4]) : 10000.0,
                        args.Length > 5 ? ParseD(args[5]) : 5.0);
                    return $"[ok] pull resistor {Unquote(args[0])} added";

                case "add_tunnel" when args.Length >= 3:
                    PythonApi.AddTunnel(Unquote(args[0]), ParseD(args[1]), ParseD(args[2]),
                        args.Length > 3 ? Unquote(args[3]) : "NET");
                    return $"[ok] tunnel {Unquote(args[0])} added";

                case "add_probe" when args.Length >= 3:
                    PythonApi.AddProbe(Unquote(args[0]), ParseD(args[1]), ParseD(args[2]),
                        args.Length > 3 ? ParseD(args[3]) : 10e6);
                    return $"[ok] probe {Unquote(args[0])} added";

                case "add_digital_transistor" or "add_transistor" when args.Length >= 3:
                    PythonApi.AddDigitalTransistor(Unquote(args[0]), ParseD(args[1]), ParseD(args[2]),
                        args.Length > 3 ? Unquote(args[3]) : "N-Type",
                        args.Length > 4 ? ParseD(args[4]) : 0.1,
                        args.Length > 5 ? ParseD(args[5]) : 10e6,
                        args.Length > 6 ? ParseD(args[6]) : 2.5);
                    return $"[ok] digital transistor {Unquote(args[0])} added";

                case "add_transmission_gate" or "add_transmissiongate" when args.Length >= 3:
                    PythonApi.AddTransmissionGate(Unquote(args[0]), ParseD(args[1]), ParseD(args[2]),
                        args.Length > 3 ? ParseD(args[3]) : 0.2,
                        args.Length > 4 ? ParseD(args[4]) : 10e6);
                    return $"[ok] transmission gate {Unquote(args[0])} added";

                case "add_bit_extender" or "add_bitextender" when args.Length >= 3:
                    PythonApi.AddBitExtender(Unquote(args[0]), ParseD(args[1]), ParseD(args[2]),
                        args.Length > 3 ? (int)ParseD(args[3]) : 1,
                        args.Length > 4 ? (int)ParseD(args[4]) : 8,
                        args.Length > 5 ? Unquote(args[5]) : "Zero");
                    return $"[ok] bit extender {Unquote(args[0])} added";

                case "add_constant" when args.Length >= 3:
                    PythonApi.AddConstant(Unquote(args[0]), ParseD(args[1]), ParseD(args[2]),
                        args.Length > 3 ? (long)ParseD(args[3]) : 1L,
                        args.Length > 4 ? ParseD(args[4]) : 5.0,
                        args.Length > 5 ? ParseD(args[5]) : 0.0);
                    return $"[ok] constant {Unquote(args[0])} added";

                case "add_dnc" or "add_do_not_connect" when args.Length >= 3:
                    PythonApi.AddDoNotConnect(Unquote(args[0]), ParseD(args[1]), ParseD(args[2]));
                    return $"[ok] DNC marker {Unquote(args[0])} added";

                case "add_lossy_transmission_line" or "add_lossy_transmissionline" when args.Length >= 3:
                    PythonApi.AddLossyTransmissionLine(Unquote(args[0]), ParseD(args[1]), ParseD(args[2]),
                        args.Length > 3 ? ParseD(args[3]) : 0.2,
                        args.Length > 4 ? ParseD(args[4]) : 250e-9,
                        args.Length > 5 ? ParseD(args[5]) : 10e-6,
                        args.Length > 6 ? ParseD(args[6]) : 100e-12,
                        args.Length > 7 ? ParseD(args[7]) : 1.0);
                    return $"[ok] lossy transmission line {Unquote(args[0])} added";

                case "add_coaxial_cable" or "add_coax" when args.Length >= 3:
                    PythonApi.AddCoaxialCable(Unquote(args[0]), ParseD(args[1]), ParseD(args[2]),
                        args.Length > 3 ? ParseD(args[3]) : 50.0,
                        args.Length > 4 ? ParseD(args[4]) : 0.66,
                        args.Length > 5 ? ParseD(args[5]) : 1.0,
                        args.Length > 6 ? ParseD(args[6]) : 0.05);
                    return $"[ok] coaxial cable {Unquote(args[0])} added";

                case "add_microstrip_line" or "add_microstrip" when args.Length >= 3:
                    PythonApi.AddMicrostripLine(Unquote(args[0]), ParseD(args[1]), ParseD(args[2]),
                        args.Length > 3 ? ParseD(args[3]) : 1.5e-3,
                        args.Length > 4 ? ParseD(args[4]) : 0.8e-3,
                        args.Length > 5 ? ParseD(args[5]) : 4.4,
                        args.Length > 6 ? ParseD(args[6]) : 0.05);
                    return $"[ok] microstrip line {Unquote(args[0])} added";

                case "toggle_logic" when args.Length >= 1:
                    PythonApi.ToggleLogic(Unquote(args[0]));
                    return $"[ok] logic component {Unquote(args[0])} toggled";

                case "set_logic" when args.Length >= 2:
                    bool lState = bool.TryParse(Unquote(args[1]), out var bS) ? bS : ParseD(args[1]) > 0.5;
                    PythonApi.SetLogic(Unquote(args[0]), lState);
                    return $"[ok] logic component {Unquote(args[0])} set to {lState}";

                case "set_logic_levels" when args.Length >= 3:
                    PythonApi.SetLogicLevels(Unquote(args[0]), ParseD(args[1]), ParseD(args[2]));
                    return $"[ok] logic levels on {Unquote(args[0])} set to High={args[1]}V, Low={args[2]}V";

                case "set_output_state" when args.Length >= 2:
                    bool oState = bool.TryParse(Unquote(args[1]), out var bO) ? bO : ParseD(args[1]) > 0.5;
                    PythonApi.SetOutputState(Unquote(args[0]), oState);
                    CircuitChanged?.Invoke();
                    return $"[ok] output component {Unquote(args[0])} state set to {oState}";

                case "rotate_component" or "rotate" when args.Length >= 2:
                    PythonApi.RotateComponent(Unquote(args[0]), ParseD(args[1]));
                    return $"[ok] component {Unquote(args[0])} rotated by {args[1]}°";

                case "stretch_component" or "stretch" or "set_scale" or "resize" when args.Length >= 3:
                    PythonApi.StretchComponent(Unquote(args[0]), ParseD(args[1]), ParseD(args[2]));
                    return $"[ok] component {Unquote(args[0])} stretched to scale ({args[1]}, {args[2]})";

                case "play" or "simulation_play":
                    PythonApi.PlaySimulation();
                    return "[ok] simulation playback started";

                case "pause" or "simulation_pause":
                    PythonApi.PauseSimulation();
                    return "[ok] simulation playback paused";

                case "play_pause" or "simulation_toggle":
                    PythonApi.ToggleSimulationPlayback();
                    return "[ok] simulation playback toggled";

                case "stop" or "simulation_stop":
                    PythonApi.StopSimulation();
                    return "[ok] simulation playback stopped";

                case "toggle_sidebar" or "toggle_component_sidebar":
                    bool? reqVis = args.Length > 0 ? (bool.TryParse(Unquote(args[0]), out var vb) ? vb : null) : null;
                    PythonApi.ToggleComponentSidebar(reqVis);
                    return $"[ok] component sidebar is now {(IsSidebarVisible ? "visible" : "hidden")}";

                case "toggle_control_sidebar":
                    bool? reqCtrlVis = args.Length > 0 ? (bool.TryParse(Unquote(args[0]), out var cvb) ? cvb : null) : null;
                    PythonApi.ToggleControlSidebar(reqCtrlVis);
                    return $"[ok] control sidebar is now {(IsControlSidebarVisible ? "visible" : "hidden")}";

                case "set_sidebar" when args.Length >= 2:
                    bool sVis = bool.TryParse(Unquote(args[1]), out var sbv) && sbv;
                    PythonApi.SetSidebarVisible(Unquote(args[0]), sVis);
                    return $"[ok] sidebar '{Unquote(args[0])}' set to {sVis}";

                case "undo":
                    PythonApi.Undo();
                    return "[ok] undo performed";

                case "redo":
                    PythonApi.Redo();
                    return "[ok] redo performed";

                case "copy_component" or "copy" when args.Length >= 1:
                    PythonApi.CopyComponent(Unquote(args[0]));
                    return $"[ok] copied component '{Unquote(args[0])}'";

                case "delete_component" or "remove_component" or "delete" or "remove" when args.Length >= 1:
                    PythonApi.DeleteComponent(Unquote(args[0]));
                    return $"[ok] deleted component '{Unquote(args[0])}'";

                case "set_breaking_current" when args.Length >= 2:
                    PythonApi.SetBreakingCurrent(Unquote(args[0]), ParseD(args[1]));
                    CircuitChanged?.Invoke();
                    return $"[ok] breaking current for '{Unquote(args[0])}' set to {args[1]}A";

                case "set_description" when args.Length >= 2:
                    PythonApi.SetDescription(Unquote(args[0]), Unquote(args[1]));
                    CircuitChanged?.Invoke();
                    return $"[ok] description for '{Unquote(args[0])}' updated";

                case "toggle_description_sidebar":
                    bool? reqDescVis = args.Length > 0 ? (bool.TryParse(Unquote(args[0]), out var dvb) ? dvb : null) : null;
                    if (reqDescVis.HasValue)
                        IsDescriptionSidebarVisible = reqDescVis.Value;
                    else
                        ToggleDescriptionSidebar();
                    return $"[ok] description sidebar is now {(IsDescriptionSidebarVisible ? "visible" : "hidden")}";

                case "set_project_name" when args.Length >= 1:
                    PythonApi.SetProjectName(Unquote(args[0]));
                    return $"[ok] project name set to '{Unquote(args[0])}'";

                case "save" or "save_project":
                    string? savePath = args.Length > 0 ? Unquote(args[0]) : null;
                    if (string.IsNullOrEmpty(savePath)) SaveProject();
                    else { SaveToPath(savePath); CurrentFilePath = savePath; ProjectName = System.IO.Path.GetFileName(savePath); }
                    return $"[ok] saved project to {ProjectName}";

                case "save_as" or "save_project_as" when args.Length >= 1:
                    string asPath = Unquote(args[0]);
                    string asFmt = args.Length >= 2 ? Unquote(args[1]) : "eee";
                    SelectedSaveFormat = asFmt;
                    SaveToPath(asPath);
                    CurrentFilePath = asPath;
                    ProjectName = System.IO.Path.GetFileName(asPath);
                    return $"[ok] saved project as {ProjectName} ({asFmt})";



                case "add_single_phase_transmission_line" when args.Length >= 1:
                {
                    var n = ParseNamedArgs(args);
                    PythonApi.AddSinglePhaseTransmissionLine(Unquote(args[0]), NamedD(n, "position_x", 100, 1), NamedD(n, "position_y", 100, 2),
                        NamedD(n, "rated_voltage", 230, 3), NamedD(n, "frequency", 50, 4), NamedD(n, "length_km", 1, 5),
                        NamedD(n, "resistance_per_km", 0.2, 6), NamedD(n, "inductance_per_km", 1e-3, 7),
                        NamedD(n, "capacitance_per_km", 10e-9, 8), NamedD(n, "conductance_per_km", 0, 9), NamedS(n, "model_type", "Short Line", 10));
                    return $"[ok] single-phase transmission line {Unquote(args[0])} added";
                }

                case "add_two_phase_transmission_line" when args.Length >= 1:
                {
                    var n = ParseNamedArgs(args);
                    PythonApi.AddTwoPhaseTransmissionLine(Unquote(args[0]), NamedD(n, "position_x", 100, 1), NamedD(n, "position_y", 100, 2),
                        NamedD(n, "rated_voltage", 240, 3), NamedD(n, "frequency", 60, 4), NamedD(n, "length_km", 1, 5),
                        NamedD(n, "phase_displacement", 90, 6), NamedS(n, "configuration", "TrueTwoPhase", 7));
                    return $"[ok] two-phase transmission line {Unquote(args[0])} added";
                }

                case "add_three_phase_transmission_line" when args.Length >= 1:
                {
                    var n = ParseNamedArgs(args);
                    PythonApi.AddThreePhaseTransmissionLine(Unquote(args[0]), NamedD(n, "position_x", 100, 1), NamedD(n, "position_y", 100, 2),
                        NamedD(n, "rated_line_voltage", 11000, 3), NamedD(n, "frequency", 50, 4), NamedD(n, "length_km", 1, 5),
                        NamedS(n, "phase_sequence", "ABC", 6), NamedS(n, "connection", "Three-wire", 7), NamedS(n, "model_type", "Short", 8));
                    return $"[ok] three-phase transmission line {Unquote(args[0])} added";
                }

                case "add_ac_transformer" when args.Length >= 1:
                {
                    var n = ParseNamedArgs(args);
                    PythonApi.AddACTransformer(Unquote(args[0]), NamedD(n, "position_x", 100, 1), NamedD(n, "position_y", 100, 2),
                        NamedD(n, "primary_voltage", 230, 3), NamedD(n, "secondary_voltage", 115, 4), NamedD(n, "apparent_power", 1000, 5),
                        NamedD(n, "frequency", 50, 6), NamedS(n, "connection", "Single-phase", 7));
                    return $"[ok] AC transformer {Unquote(args[0])} added";
                }

                case "add_synchronous_generator" when args.Length >= 1:
                {
                    var n = ParseNamedArgs(args);
                    PythonApi.AddSynchronousGenerator(Unquote(args[0]), NamedD(n, "position_x", 100, 1), NamedD(n, "position_y", 100, 2),
                        NamedD(n, "rated_voltage", 400, 3), NamedD(n, "rated_frequency", 50, 4), NamedD(n, "rated_power", 10000, 5),
                        (int)NamedD(n, "phases", 3, 6), (int)NamedD(n, "poles", 4, 7));
                    return $"[ok] synchronous generator {Unquote(args[0])} added";
                }

                case "add_induction_motor" when args.Length >= 1:
                {
                    var n = ParseNamedArgs(args);
                    PythonApi.AddInductionMotor(Unquote(args[0]), NamedD(n, "position_x", 100, 1), NamedD(n, "position_y", 100, 2),
                        NamedD(n, "rated_voltage", 400, 3), NamedD(n, "rated_power", 1500, 4), NamedD(n, "frequency", 50, 5), (int)NamedD(n, "poles", 4, 6));
                    return $"[ok] induction motor {Unquote(args[0])} added";
                }

                case "add_ac_bulb" when args.Length >= 1:
                {
                    var n = ParseNamedArgs(args);
                    PythonApi.AddACBulb(Unquote(args[0]), NamedD(n, "position_x", 100, 1), NamedD(n, "position_y", 100, 2),
                        NamedD(n, "rated_voltage", 230, 3), NamedD(n, "rated_power", 60, 4), NamedB(n, "initially_on", true, 5), NamedD(n, "dimming_percent", 100, 6));
                    return $"[ok] AC bulb {Unquote(args[0])} added";
                }

                case "add_ac_fan" when args.Length >= 1:
                {
                    var n = ParseNamedArgs(args);
                    PythonApi.AddACFan(Unquote(args[0]), NamedD(n, "position_x", 100, 1), NamedD(n, "position_y", 100, 2),
                        NamedD(n, "rated_voltage", 230, 3), NamedD(n, "rated_power", 75, 4), NamedD(n, "power_factor", 0.8, 5), (int)NamedD(n, "selected_speed", 3, 6));
                    return $"[ok] AC fan {Unquote(args[0])} added";
                }

                case "add_solar_panel" when args.Length >= 1:
                {
                    var n = ParseNamedArgs(args);
                    PythonApi.AddSolarPanel(Unquote(args[0]), NamedD(n, "position_x", 100, 1), NamedD(n, "position_y", 100, 2),
                        NamedD(n, "maximum_power_rating", 300, 3), NamedD(n, "open_circuit_voltage", 40, 4),
                        NamedD(n, "short_circuit_current", 9, 5), NamedD(n, "irradiance", 1000, 6));
                    return $"[ok] solar panel {Unquote(args[0])} added";
                }

                case "add_battery" when args.Length >= 1:
                {
                    var n = ParseNamedArgs(args);
                    PythonApi.AddBattery(Unquote(args[0]), NamedD(n, "position_x", 100, 1), NamedD(n, "position_y", 100, 2),
                        NamedD(n, "nominal_voltage", 12, 3), NamedD(n, "capacity_ah", 100, 4),
                        NamedD(n, "initial_soc_percent", 80, 5), NamedS(n, "chemistry", "Lithium-ion", 6));
                    return $"[ok] battery {Unquote(args[0])} added";
                }

                default:
                    return $"[unknown method] {method}";
            }
        }
        catch (Exception ex)
        {
            return $"[error] {ex.Message}";
        }
    }

    private static Dictionary<string, string> ParseNamedArgs(string[] args)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < args.Length; i++)
        {
            var arg = args[i].Trim();
            var eqIdx = arg.IndexOf('=');
            if (eqIdx > 0)
            {
                var key = arg[..eqIdx].Trim();
                var val = arg[(eqIdx + 1)..].Trim();
                if (key.Equals("position", StringComparison.OrdinalIgnoreCase) && val.StartsWith('(') && val.EndsWith(')'))
                {
                    var coords = val.Trim('(', ')').Split(',');
                    if (coords.Length >= 2)
                    {
                        map["position_x"] = coords[0].Trim();
                        map["position_y"] = coords[1].Trim();
                    }
                }
                else
                {
                    map[key] = val;
                }
            }
            else
            {
                map[$"arg_{i}"] = arg;
            }
        }
        return map;
    }

    private static double NamedD(Dictionary<string, string> named, string key, double fallback = 0, int posIndex = -1)
    {
        if (named.TryGetValue(key, out string? value)) return ParseD(Unquote(value));
        if (posIndex >= 0 && named.TryGetValue($"arg_{posIndex}", out string? posVal)) return ParseD(Unquote(posVal));
        return fallback;
    }

    private static string NamedS(Dictionary<string, string> named, string key, string fallback, int posIndex = -1)
    {
        if (named.TryGetValue(key, out string? value)) return Unquote(value);
        if (posIndex >= 0 && named.TryGetValue($"arg_{posIndex}", out string? posVal)) return Unquote(posVal);
        return fallback;
    }

    private static bool NamedB(Dictionary<string, string> named, string key, bool fallback, int posIndex = -1)
    {
        if (named.TryGetValue(key, out string? value) && bool.TryParse(Unquote(value), out bool parsed)) return parsed;
        if (posIndex >= 0 && named.TryGetValue($"arg_{posIndex}", out string? posVal) && bool.TryParse(Unquote(posVal), out bool posParsed)) return posParsed;
        return fallback;
    }

    private static string[] SplitArgs(string argsStr)
    {
        // Split top-level commas while preserving quoted text and nested tuples.
        var result = new System.Collections.Generic.List<string>();
        int depth = 0;
        char quote = '\0';
        bool escaped = false;
        var cur = new StringBuilder();
        foreach (char c in argsStr)
        {
            if (escaped)
            {
                cur.Append(c);
                escaped = false;
                continue;
            }
            if (c == '\\' && quote != '\0')
            {
                cur.Append(c);
                escaped = true;
                continue;
            }
            if (quote != '\0')
            {
                cur.Append(c);
                if (c == quote) quote = '\0';
                continue;
            }
            if (c is '\'' or '"')
            {
                quote = c;
                cur.Append(c);
            }
            else if (c == '(')
            {
                depth++;
                cur.Append(c);
            }
            else if (c == ')')
            {
                depth--;
                cur.Append(c);
            }
            else if (c == ',' && depth == 0)
            {
                result.Add(cur.ToString().Trim());
                cur.Clear();
            }
            else cur.Append(c);
        }
        if (cur.Length > 0) result.Add(cur.ToString().Trim());
        return result.ToArray();
    }

    private static string Unquote(string s)
    {
        string value = s.Trim();
        if (value.Length >= 2 && value[0] == '"' && value[^1] == '"')
        {
            try { return JsonSerializer.Deserialize<string>(value) ?? string.Empty; }
            catch { /* Fall back to the existing permissive quote trimming for legacy scripts. */ }
        }
        return value.Trim('\'').Trim('"');
    }

    private static double ParseD(string s) =>
        double.TryParse(s.Trim(), System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture, out var v) ? v : 0;

    private static object ParseScriptValue(string raw)
    {
        string value = Unquote(raw);
        if (bool.TryParse(value, out var boolean)) return boolean;
        if (long.TryParse(value, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var integer)) return integer;
        if (double.TryParse(value, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var number)) return number;
        return value;
    }

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
        if (r.Message.Contains("Transient waveform capture failed", StringComparison.OrdinalIgnoreCase))
            sb.AppendLine($"\n{r.Message}");
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
    public bool IsCustomCircuit => Category.Equals("My Circuits", StringComparison.OrdinalIgnoreCase);
    public bool HasCustomSvg => IsCustomCircuit && SvgIcon.Contains("<svg", StringComparison.OrdinalIgnoreCase);
    public bool IsCustomWithoutSvg => IsCustomCircuit && !HasCustomSvg;
    public HtmlWebViewSource? CustomSvgSource => HasCustomSvg
        ? new HtmlWebViewSource
        {
            Html = $"<html><head><meta name='viewport' content='width=device-width, initial-scale=1'/><style>html,body{{margin:0;width:100%;height:100%;background:transparent;overflow:hidden}}svg{{width:100%;height:100%}}</style></head><body>{SvgIcon}</body></html>"
        }
        : null;
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

/// <summary>A named group of component palette items shown with a category header.</summary>
public class ComponentPaletteGroup
{
    public string Header { get; }
    public IReadOnlyList<ComponentPaletteItem> Items { get; }
    public ComponentPaletteGroup(string header, IEnumerable<ComponentPaletteItem> items)
    {
        Header = header;
        Items  = items.ToList();
    }
}

public partial class CustomCircuitPortDraft : ObservableObject
{
    public Guid InnerComponentId { get; }
    public string InnerPinId { get; }
    public string ComponentReference { get; }
    public string OriginalPinName { get; }
    public double RelativeX { get; }
    public double RelativeY { get; }

    [ObservableProperty]
    public partial string Name { get; set; } = string.Empty;

    public CustomCircuitPortDraft(Guid innerComponentId, string innerPinId, string componentReference,
        string originalPinName, string name, double relativeX, double relativeY)
    {
        InnerComponentId = innerComponentId;
        InnerPinId = innerPinId;
        ComponentReference = componentReference;
        OriginalPinName = originalPinName;
        Name = name;
        RelativeX = relativeX;
        RelativeY = relativeY;
    }
}
