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
    public partial bool IsControlSidebarVisible { get; set; } = true;

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
    public partial ComponentNode? EditingComponent { get; set; }

    /// <summary>The most recently selected component on the canvas (for toolbar actions like Rotate, Copy, Delete).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelectedComponent))]
    public partial ComponentNode? SelectedComponent { get; set; }

    public bool HasSelectedComponent => SelectedComponent != null;

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
        new ComponentPaletteItem("VCVS",             "E",   "μ",  "#56D364", "Electrical",  "Voltage-Controlled Voltage Source", "vcvs.svg"),
        new ComponentPaletteItem("VCCS",             "G",   "Gm", "#56D364", "Electrical",  "Voltage-Controlled Current Source", "vccs.svg"),
        new ComponentPaletteItem("CCVS",             "H",   "Rm", "#79C0FF", "Electrical",  "Current-Controlled Voltage Source", "ccvs.svg"),
        new ComponentPaletteItem("CCCS",             "F",   "β",  "#79C0FF", "Electrical",  "Current-Controlled Current Source", "cccs.svg"),
        new ComponentPaletteItem("VoltageSwitch",    "S",   "SW", "#E3B341", "Electrical",  "Voltage-Controlled Switch", "voltageswitch.svg"),
        new ComponentPaletteItem("CurrentSwitch",    "W",   "SW", "#E3B341", "Electrical",  "Current-Controlled Switch", "currentswitch.svg"),
        new ComponentPaletteItem("TransmissionLine", "T",   "Ω",  "#79C0FF", "Electrical",  "Lossless Transmission Line", "transmissionline.svg"),
        new ComponentPaletteItem("VoltageDelay",     "VD",  "s",  "#56D364", "Electrical",  "Voltage Delay Element",      "voltagedelay.svg"),
        new ComponentPaletteItem("Bulb",             "💡",  "LAMP","#F59E0B", "Electrical",  "Incandescent Light Bulb",    "bulb.svg"),

        // ── Electronics (Semiconductors & Active) ────────────────────────────
        new ComponentPaletteItem("LED",           "💡",  "LED","#E3B341", "Electronics", "Light Emitting Diode",       "led.svg"),
        new ComponentPaletteItem("LogicInput",    "🔲",  "IN", "#56D364", "Electronics", "Toggle Logic Input (0/1)",   "logicinput.svg"),
        new ComponentPaletteItem("LogicOutput",   "🎯",  "OUT","#79C0FF", "Electronics", "Digital Logic Probe (0/1)",  "logicoutput.svg"),
        new ComponentPaletteItem("NPN",           "Q",   "NPN","#FFA657", "Electronics", "BJT NPN Transistor",         "bjt_npn.svg"),
        new ComponentPaletteItem("PNP",           "Q",   "PNP","#FFA657", "Electronics", "BJT PNP Transistor",    "bjt_pnp.svg"),
        new ComponentPaletteItem("JFET",          "J",   "FET","#FFA657", "Electronics", "Junction Field-Effect",  "jfet.svg"),
        new ComponentPaletteItem("NMOS",          "M",   "NMOS","#D2A8FF", "Electronics", "N-Channel MOSFET",     "nmos.svg"),
        new ComponentPaletteItem("PMOS",          "M",   "PMOS","#D2A8FF", "Electronics", "P-Channel MOSFET",     "pmos.svg"),
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

    /// <summary>Palette grouped by category for the sidebar 2-per-row grid layout.</summary>
    public IReadOnlyList<ComponentPaletteGroup> GroupedPalette { get; } = new List<ComponentPaletteGroup>
    {
        new ComponentPaletteGroup("⚡ Passives", new[]
        {
            new ComponentPaletteItem("Resistor",      "R",  "Ω",  "#F78166", "Passives",  "Linear resistor",          "resistor.svg"),
            new ComponentPaletteItem("Capacitor",     "C",  "F",  "#79C0FF", "Passives",  "Capacitor",                "capacitor.svg"),
            new ComponentPaletteItem("Inductor",      "L",  "H",  "#D2A8FF", "Passives",  "Inductor",                 "inductor.svg"),
            new ComponentPaletteItem("MutualInductance","K","k",  "#D2A8FF", "Passives",  "Mutual coupled inductors", "mutualinductance.svg"),
        }),
        new ComponentPaletteGroup("🔋 Sources", new[]
        {
            new ComponentPaletteItem("VoltageSource", "V",  "V",  "#56D364", "Sources",   "Voltage source",           "voltagesource.svg"),
            new ComponentPaletteItem("CurrentSource", "I",  "A",  "#56D364", "Sources",   "Current source",           "currentsource.svg"),
            new ComponentPaletteItem("VoltageDelay",  "VD", "s",  "#56D364", "Sources",   "Voltage Delay Element",    "voltagedelay.svg"),
            new ComponentPaletteItem("Ground",        "⏚",  "GND","#8B949E", "Sources",   "Circuit ground",           "ground.svg"),
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
        }),
        new ComponentPaletteGroup("⚙ Switches", new[]
        {
            new ComponentPaletteItem("VoltageSwitch", "S", "SW", "#E3B341", "Switches", "Voltage-Controlled Switch", "voltageswitch.svg"),
            new ComponentPaletteItem("CurrentSwitch", "W", "SW", "#E3B341", "Switches", "Current-Controlled Switch", "currentswitch.svg"),
        }),
        new ComponentPaletteGroup("📡 Transmission", new[]
        {
            new ComponentPaletteItem("TransmissionLine", "T",  "Ω", "#79C0FF", "Transmission", "Lossless Transmission Line", "transmissionline.svg"),
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
        new ComponentPaletteGroup("🗂 Schematic", new[]
        {
            new ComponentPaletteItem("Wire",           "—",  "W",   "#58A6FF", "Schematic", "Wire segment",      "wire.svg"),
            new ComponentPaletteItem("Junction",       "•",  "J",   "#58A6FF", "Schematic", "Wire junction",     "junction.svg"),
            new ComponentPaletteItem("Label",          "🏷", "LBL", "#56D364", "Schematic", "Net label",         "label.svg"),
            new ComponentPaletteItem("TextAnnotation", "🗎", "TXT", "#8B949E", "Schematic", "Text note",         "textannotation.svg"),
            new ComponentPaletteItem("Connector",      "⑉",  "HDR", "#E3B341", "Schematic", "Header connector",  "connector.svg"),
        }),
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
        var navSimSettings = new ButtonNode("NavSimSettings", "Simulation Settings", () => _ = OpenSimSettings());
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
    public async Task OpenExamplesPage()
    {
        try
        {
            await Shell.Current.GoToAsync("examples");
        }
        catch
        {
            StatusText = "Opening Examples";
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
    public void SaveProjectAs()
    {
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
    public void ConfirmSaveAs()
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
            string docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            if (string.IsNullOrWhiteSpace(docs) || !System.IO.Directory.Exists(docs))
            {
                docs = FileSystem.AppDataDirectory;
            }
            string path = System.IO.Path.Combine(docs, "EESimulator");
            if (!System.IO.Directory.Exists(path))
            {
                System.IO.Directory.CreateDirectory(path);
            }
            return path;
        }
        catch
        {
            return FileSystem.AppDataDirectory;
        }
    }

    private void SaveToPath(string filePath)
    {
        var dir = System.IO.Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(dir) && !System.IO.Directory.Exists(dir))
        {
            System.IO.Directory.CreateDirectory(dir);
        }

        string ext = System.IO.Path.GetExtension(filePath).ToLowerInvariant();
        switch (ext)
        {
            case ".eee":
                EeePlatformSerializer.SaveToFile(Circuit, filePath);
                break;
            case ".eesim":
            case ".json":
                EesimSerializer.SaveToFile(Circuit, filePath);
                break;
            case ".kicad_sch":
                KiCadExporter.SaveToFile(Circuit, filePath);
                break;
            case ".cir":
            case ".sp":
                System.IO.File.WriteAllText(filePath, SpiceNetlistExporter.ExportToSpice(Circuit, _simSettings));
                break;
            case ".py":
                System.IO.File.WriteAllText(filePath, PythonCodeGenerator.GenerateFullScript(Circuit));
                break;
            default:
                EeePlatformSerializer.SaveToFile(Circuit, filePath);
                break;
        }
    }


    [RelayCommand]
    private void RotateSelectedComponent()
    {
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
        CircuitChanged?.Invoke();
        StatusText = $"Deleted {comp.Reference}";
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
        StatusText = $"Updated description & properties for {SelectedComponent.Reference}";
    }

    [RelayCommand]
    private void ToggleViewport() => IsScriptViewActive = !IsScriptViewActive;

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
            _ = SimulateAsync();
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
        SelectedComponent = comp;
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


