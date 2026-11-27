using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using EESimulator.Commands;
using EESimulator.Commands.Commands;
using EESimulator.Core.Components;
using EESimulator.Core.Components.Base;
using EESimulator.Core.Components.Electrical;
using EESimulator.Core.Components.Electronics;
using EESimulator.Core.Models;
using EESimulator.Core.UI;
using EESimulator.Engine;
using EESimulator.Engine.Simulation;

namespace EESimulator.Scripting.API;

/// <summary>
/// Python API exposed to scripts (equivalent to Blender's bpy API), allowing programmatic
/// manipulation of circuits, components, connections, viewports, simulation execution,
/// and complete UI hierarchy inspection and control.
/// </summary>
public class PythonApi
{
    private readonly CommandManager _commandManager;
    private readonly CircuitNode _circuit;
    private readonly SimulationManager _simulationManager;

    public CircuitNode Circuit => _circuit;
    public UIRootNode UI { get; }

    public event Action<string>? OnWindowOpenRequested;
    public event Action<string>? OnActiveViewportChanged;
    public event Action<string, string>? OnControlButtonRegistered;
    public event Action<string>? OnActiveToolChanged;
    public event Action<bool?>? OnSidebarToggled;
    public event Action<bool?>? OnControlSidebarToggled;
    public event Action<bool?>? OnLeftToolbarToggled;
    public event Action<string, bool>? OnSidebarVisibilityChanged;
    public event Action? OnSimulationPlayRequested;
    public event Action? OnSimulationPauseRequested;
    public event Action? OnSimulationToggleRequested;
    public event Action? OnSimulationStopRequested;
    public event Action? OnUndoRequested;
    public event Action? OnRedoRequested;
    public event Action<string>? OnProjectNameChanged;
    public event Action? OnWaveformDrawerToggleRequested;
    public event Action? OnViewToggleRequested;
    public event Action<string, double>? OnComponentRotateRequested;
    public event Action<string, double, double>? OnComponentStretchRequested;
    public event Action<string>? OnLogicToggled;
    public event Action? OnDescriptionSidebarToggled;
    public event Action<string>? OnComponentDeleted;
    public event Action? OnConnectionDeleted;
    public event Action<string>? OnComponentCopied;
    public event Action<string?>? OnSaveRequested;
    public event Action<string, string>? OnSaveAsRequested;
    public event Action<string?, string>? OnCircuitImageSaveRequested;
    public event Action<string?, int, string, string, double?, bool>? OnVideoRecordingStartRequested;
    public event Action? OnVideoRecordingStopRequested;
    public event Action<bool>? OnWaveformDrawerVisibilityRequested;
    public event Action? OnCloseSidebarsRequested;
    public event Action? OnOpenSidebarsRequested;
    public event Action<string, object?>? OnWorkspaceSettingChanged;
    public event Action? OnWorkspaceSettingsResetRequested;
    public event Action<string>? OnSubtitleChanged;

    public string SubtitleText { get; private set; } = string.Empty;

    public bool IsVideoRecording { get; private set; }
    public int VideoFrameRate { get; private set; } = 30;
    public string VideoFormat { get; private set; } = "GIF";

    public PythonApi(CommandManager commandManager, CircuitNode circuit, SimulationManager? simulationManager = null, UIRootNode? uiRoot = null)
    {
        _commandManager = commandManager ?? throw new ArgumentNullException(nameof(commandManager));
        _circuit = circuit ?? throw new ArgumentNullException(nameof(circuit));
        _simulationManager = simulationManager ?? new SimulationManager();
        UI = uiRoot ?? new UIRootNode();
    }

    // Circuit Operations
    public void ClearCircuit()
    {
        while (_circuit.Children.Count > 0)
        {
            _circuit.RemoveChild(_circuit.Children[0]);
        }
        _circuit.InvalidateStructure();
    }

    public void AddComponent(string type, string name, double x, double y, double param = 0)
    {
        var cmd = new AddComponentCommand(_circuit, type, name, x, y, param)
        {
            Origin = CommandOrigin.Python
        };
        _commandManager.ExecuteCommand(cmd);
    }

    public void AddComponentAtGrid(string type, string name, int row, int column, double param = 0)
    {
        if (row < 1) throw new ArgumentOutOfRangeException(nameof(row), "Rows are 1-based positive integers.");
        if (column < 1) throw new ArgumentOutOfRangeException(nameof(column), "Columns are 1-based positive integers.");
        AddComponent(type, name, 0, 0, param);
        SetLogicalGridPosition(name, row, column);
    }

    public void AddTtlPackage(string partNumber, string name, double x, double y)
    {
        if (!EESimulator.Core.Components.Electronics.TtlPackageComponent.IsSupported(partNumber))
            throw new ArgumentException($"Unsupported 74-series part number '{partNumber}'.", nameof(partNumber));
        AddComponent(partNumber, name, x, y);
    }

    public void AddResistor(string name, double x, double y, double resistance = 1000.0, double tc1 = 0.0, double tc2 = 0.0, double temp = 27.0)
    {
        AddComponent("resistor", name, x, y, resistance);
        if (tc1 != 0.0) SetProperty(name, "TC1", tc1);
        if (tc2 != 0.0) SetProperty(name, "TC2", tc2);
        if (temp != 27.0) SetProperty(name, "Temperature", temp);
    }

    public void AddCapacitor(string name, double x, double y, double capacitance = 1e-6, double ic = 0.0, double tc1 = 0.0, double tc2 = 0.0, double temp = 27.0)
    {
        AddComponent("capacitor", name, x, y, capacitance);
        if (ic != 0.0) SetProperty(name, "IC", ic);
        if (tc1 != 0.0) SetProperty(name, "TC1", tc1);
        if (tc2 != 0.0) SetProperty(name, "TC2", tc2);
        if (temp != 27.0) SetProperty(name, "Temperature", temp);
    }

    public void AddInductor(string name, double x, double y, double inductance = 1e-3, double ic = 0.0, double seriesMultiplier = 1.0, double parallelMultiplier = 1.0)
    {
        AddComponent("inductor", name, x, y, inductance);
        if (ic != 0.0) SetProperty(name, "IC", ic);
        if (seriesMultiplier != 1.0) SetProperty(name, "SeriesMultiplier", seriesMultiplier);
        if (parallelMultiplier != 1.0) SetProperty(name, "ParallelMultiplier", parallelMultiplier);
    }

    public void AddMutualInductance(string name, double x, double y, string ind1 = "L1", string ind2 = "L2", double coupling = 0.99)
    {
        AddComponent("mutualinductance", name, x, y, coupling);
        SetCustomProperty(name, "Inductor1", ind1);
        SetCustomProperty(name, "Inductor2", ind2);
    }

    public void AddVoltageSource(string name, double x, double y, double dc = 5.0, double acMag = 0.0, double acPhase = 0.0, string waveform = "DC")
    {
        AddComponent("voltagesource", name, x, y, dc);
        if (acMag != 0.0) SetProperty(name, "AcMagnitude", acMag);
        if (acPhase != 0.0) SetProperty(name, "AcPhase", acPhase);
        if (!string.Equals(waveform, "DC", StringComparison.OrdinalIgnoreCase))
            SetCustomProperty(name, "WaveformType", waveform);
    }

    public void AddCurrentSource(string name, double x, double y, double dc = 0.001, double acMag = 0.0, double acPhase = 0.0, string waveform = "DC")
    {
        AddComponent("currentsource", name, x, y, dc);
        if (acMag != 0.0) SetProperty(name, "AcMagnitude", acMag);
        if (acPhase != 0.0) SetProperty(name, "AcPhase", acPhase);
        if (!string.Equals(waveform, "DC", StringComparison.OrdinalIgnoreCase))
            SetCustomProperty(name, "WaveformType", waveform);
    }

    public void AddDiode(string name, double x, double y, double isat = 1e-14, double n = 1.0, double bv = 0.0, double rs = 0.0, double cj0 = 0.0, double tt = 0.0, double ibv = 1e-3)
    {
        AddComponent("diode", name, x, y, isat);
        if (n != 1.0) SetProperty(name, "N", n);
        if (bv != 0.0) SetProperty(name, "Bv", bv);
        if (rs != 0.0) SetProperty(name, "Rs", rs);
        if (cj0 != 0.0) SetProperty(name, "Cj0", cj0);
        if (tt != 0.0) SetProperty(name, "Tt", tt);
        if (ibv != 1e-3) SetProperty(name, "Ibv", ibv);
    }

    public void AddVernierDiode(string name, double x, double y, double vernierPosition = 50, double minimumForwardVoltage = 0.55, double maximumForwardVoltage = 0.75)
    {
        AddComponent("vernierdiode", name, x, y, 1e-14);
        SetComponentProperty(name, "VernierPosition", vernierPosition);
        SetComponentProperty(name, "MinimumForwardVoltage", minimumForwardVoltage);
        SetComponentProperty(name, "MaximumForwardVoltage", maximumForwardVoltage);
    }

    public void AddBJT(string name, double x, double y, string polarity = "NPN", double bf = 100.0, double br = 1.0, double isat = 1e-14, double vaf = 0.0)
    {
        AddComponent("bjt", name, x, y, bf);
        var comp = _circuit.FindChild<EESimulator.Core.Components.Electronics.BJTComponent>(name);
        if (comp != null)
        {
            comp.Polarity = polarity.Equals("PNP", StringComparison.OrdinalIgnoreCase)
                ? EESimulator.Core.Components.Electronics.BjtPolarity.PNP
                : EESimulator.Core.Components.Electronics.BjtPolarity.NPN;
        }
        if (br != 1.0) SetProperty(name, "Br", br);
        if (isat != 1e-14) SetProperty(name, "Is", isat);
        if (vaf != 0.0) SetProperty(name, "Vaf", vaf);
    }

    public void AddNPN(string name, double x, double y, double bf = 100.0, double br = 1.0, double isat = 1e-14, double vaf = 0.0)
    {
        AddBJT(name, x, y, "NPN", bf, br, isat, vaf);
    }

    public void AddPNP(string name, double x, double y, double bf = 100.0, double br = 1.0, double isat = 1e-14, double vaf = 0.0)
    {
        AddBJT(name, x, y, "PNP", bf, br, isat, vaf);
    }

    public void AddMOSFET(string name, double x, double y, string channel = "NChannel", double vto = 2.0, double kp = 2e-5, double w = 1e-4, double l = 1e-6, double lambda = 0.0)
    {
        AddComponent("mosfet", name, x, y, vto);
        var comp = _circuit.FindChild<EESimulator.Core.Components.Electronics.MOSFETComponent>(name);
        if (comp != null)
        {
            comp.Channel = channel.Equals("PChannel", StringComparison.OrdinalIgnoreCase)
                ? EESimulator.Core.Components.Electronics.MosfetChannel.PChannel
                : EESimulator.Core.Components.Electronics.MosfetChannel.NChannel;
        }
        if (kp != 2e-5) SetProperty(name, "Kp", kp);
        if (w != 1e-4) SetProperty(name, "W", w);
        if (l != 1e-6) SetProperty(name, "L", l);
        if (lambda != 0.0) SetProperty(name, "Lambda", lambda);
    }

    public void AddNMOS(string name, double x, double y, double vto = 2.0, double kp = 2e-5, double w = 1e-4, double l = 1e-6, double lambda = 0.0)
    {
        AddMOSFET(name, x, y, "NChannel", vto, kp, w, l, lambda);
    }

    public void AddPMOS(string name, double x, double y, double vto = -2.0, double kp = 2e-5, double w = 1e-4, double l = 1e-6, double lambda = 0.0)
    {
        AddMOSFET(name, x, y, "PChannel", vto, kp, w, l, lambda);
    }

    public void AddJFET(string name, double x, double y, string channel = "NChannel", double vto = -2.0, double beta = 1e-4, double lambda = 0.0)
    {
        AddComponent("jfet", name, x, y, vto);
        var comp = _circuit.FindChild<EESimulator.Core.Components.Electronics.JFETComponent>(name);
        if (comp != null)
        {
            comp.Channel = channel.Equals("PChannel", StringComparison.OrdinalIgnoreCase)
                ? EESimulator.Core.Components.Electronics.JfetChannel.PChannel
                : EESimulator.Core.Components.Electronics.JfetChannel.NChannel;
        }
        if (beta != 1e-4) SetProperty(name, "Beta", beta);
        if (lambda != 0.0) SetProperty(name, "Lambda", lambda);
    }

    public void AddOpAmp(string name, double x, double y, double aol = 100000.0, double gbw = 1e6, double slewRate = 0.5, double vcc = 15.0, double vee = -15.0)
    {
        AddComponent("opamp", name, x, y, aol);
        if (gbw != 1e6) SetProperty(name, "GBW", gbw);
        if (slewRate != 0.5) SetProperty(name, "SlewRate", slewRate);
        if (vcc != 15.0) SetProperty(name, "Vcc", vcc);
        if (vee != -15.0) SetProperty(name, "Vee", vee);
    }

    public void AddVCVS(string name, double x, double y, double voltageGain = 1.0)
    {
        AddComponent("vcvs", name, x, y, voltageGain);
    }

    public void AddVCCS(string name, double x, double y, double transconductance = 0.001)
    {
        AddComponent("vccs", name, x, y, transconductance);
    }

    public void AddCCVS(string name, double x, double y, double transresistance = 1000.0, string controllingSource = "")
    {
        AddComponent("ccvs", name, x, y, transresistance);
        if (!string.IsNullOrWhiteSpace(controllingSource))
            SetCustomProperty(name, "ControllingSource", controllingSource);
    }

    public void AddCCCS(string name, double x, double y, double currentGain = 1.0, string controllingSource = "")
    {
        AddComponent("cccs", name, x, y, currentGain);
        if (!string.IsNullOrWhiteSpace(controllingSource))
            SetCustomProperty(name, "ControllingSource", controllingSource);
    }

    public void AddVoltageSwitch(string name, double x, double y, double ron = 1.0, double roff = 1e6, double threshold = 1.0, double hysteresis = 0.1)
    {
        AddComponent("voltageswitch", name, x, y, ron);
        if (roff != 1e6) SetProperty(name, "Roff", roff);
        if (threshold != 1.0) SetProperty(name, "Threshold", threshold);
        if (hysteresis != 0.1) SetProperty(name, "Hysteresis", hysteresis);
    }

    public void AddCurrentSwitch(string name, double x, double y, double ron = 1.0, double roff = 1e6, double threshold = 0.001, double hysteresis = 0.0, string controllingSource = "")
    {
        AddComponent("currentswitch", name, x, y, ron);
        if (roff != 1e6) SetProperty(name, "Roff", roff);
        if (threshold != 0.001) SetProperty(name, "Threshold", threshold);
        if (hysteresis != 0.0) SetProperty(name, "Hysteresis", hysteresis);
        if (!string.IsNullOrWhiteSpace(controllingSource))
            SetCustomProperty(name, "ControllingSource", controllingSource);
    }

    public void AddTransmissionLine(string name, double x, double y, double z0 = 50.0, double td = 1e-9, double f = 1e9, double nl = 0.25)
    {
        AddComponent("transmissionline", name, x, y, z0);
        if (td != 1e-9) SetProperty(name, "TD", td);
        if (f != 1e9) SetProperty(name, "F", f);
        if (nl != 0.25) SetProperty(name, "NL", nl);
    }

    public void AddVoltageDelay(string name, double x, double y, double delay = 1e-6)
    {
        AddComponent("voltagedelay", name, x, y, delay);
    }

    public void AddClock(string name, double x, double y, double frequency = 1000.0, double dutyCycle = 0.5, double highVoltage = 5.0, double lowVoltage = 0.0, int highDuration = 1, int lowDuration = 1, int phase = 0)
    {
        AddComponent("clock", name, x, y, frequency);
        if (dutyCycle != 0.5) SetProperty(name, "DutyCycle", dutyCycle);
        if (highVoltage != 5.0) SetProperty(name, "HighVoltage", highVoltage);
        if (lowVoltage != 0.0) SetProperty(name, "LowVoltage", lowVoltage);
        if (highDuration != 1) SetCustomProperty(name, "HighDuration", highDuration);
        if (lowDuration != 1) SetCustomProperty(name, "LowDuration", lowDuration);
        if (phase != 0) SetCustomProperty(name, "Phase", phase);
    }

    public void AddCounter(string name, double x, double y, int bitWidth = 3, long maxValue = 5, long initialValue = 1, string goalAction = "Load")
    {
        AddComponent("counter", name, x, y, bitWidth);
        SetComponentProperty(name, "MaxValue", maxValue);
        SetComponentProperty(name, "Value", initialValue);
        SetComponentProperty(name, "GoalAction", goalAction);
    }

    public void AddROM(string name, double x, double y, int addressBitWidth = 3, int dataBitWidth = 8, string contentsHex = "")
    {
        AddComponent("rom", name, x, y, addressBitWidth);
        SetComponentProperty(name, "DataBitWidth", dataBitWidth);
        SetComponentProperty(name, "ContentsHex", contentsHex);
    }

    public void AddSevenSegment(string name, double x, double y)
    {
        AddComponent("sevensegment", name, x, y, 0);
    }

    public void AddPin(string name, double x, double y, string direction = "Input", int bitWidth = 1, long value = 0, string behavior = "Tristate", long initialValue = 0, string radix = "Binary")
    {
        AddComponent("pin", name, x, y, value);
        SetCustomProperty(name, "Direction", direction);
        if (bitWidth != 1) SetProperty(name, "BitWidth", bitWidth);
        if (behavior != "Tristate") SetCustomProperty(name, "Behavior", behavior);
        if (initialValue != 0) SetCustomProperty(name, "InitialValue", initialValue);
        if (radix != "Binary") SetCustomProperty(name, "Radix", radix);
    }

    public void AddPower(string name, double x, double y, double voltage = 5.0, int bitWidth = 1)
    {
        AddComponent("power", name, x, y, voltage);
        if (bitWidth != 1) SetProperty(name, "BitWidth", bitWidth);
    }

    public void AddPullResistor(string name, double x, double y, string mode = "PullUp", double resistance = 10000.0, double railVoltage = 5.0)
    {
        AddComponent("pullresistor", name, x, y, resistance);
        SetCustomProperty(name, "Mode", mode);
        if (railVoltage != 5.0) SetProperty(name, "RailVoltage", railVoltage);
    }

    public void AddTunnel(string name, double x, double y, string label = "", int bitWidth = 1)
    {
        AddComponent("tunnel", name, x, y, 0);
        if (!string.IsNullOrWhiteSpace(label))
            SetCustomProperty(name, "TunnelLabel", label);
        if (bitWidth != 1) SetProperty(name, "BitWidth", bitWidth);
    }

    public void AddProbe(string name, double x, double y, double inputImpedance = 10e6, string radix = "Binary")
    {
        AddComponent("probe", name, x, y, inputImpedance);
        if (radix != "Binary") SetCustomProperty(name, "Radix", radix);
    }

    public void AddDigitalTransistor(string name, double x, double y, string type = "NType", double ron = 0.1, double roff = 10e6, double threshold = 2.5, string gateLocation = "TopRight", int bitWidth = 1)
    {
        AddComponent("transistor", name, x, y, threshold);
        SetCustomProperty(name, "TransistorType", type);
        if (ron != 0.1) SetProperty(name, "Ron", ron);
        if (roff != 10e6) SetProperty(name, "Roff", roff);
        if (gateLocation != "TopRight") SetCustomProperty(name, "GateLocation", gateLocation);
        if (bitWidth != 1) SetProperty(name, "BitWidth", bitWidth);
    }

    public void AddTransmissionGate(string name, double x, double y, double ron = 0.2, double roff = 10e6, string selectLoc = "TopRight", int bitWidth = 1)
    {
        AddComponent("transmissiongate", name, x, y, ron);
        if (roff != 10e6) SetProperty(name, "Roff", roff);
        if (selectLoc != "TopRight") SetCustomProperty(name, "SelectLocation", selectLoc);
        if (bitWidth != 1) SetProperty(name, "BitWidth", bitWidth);
    }

    public void AddBitExtender(string name, double x, double y, int inWidth = 1, int outWidth = 8, string type = "ZeroExtend")
    {
        AddComponent("bitextender", name, x, y, inWidth);
        SetProperty(name, "OutputBitWidth", outWidth);
        SetCustomProperty(name, "ExtensionType", type);
    }

    public void AddConstant(string name, double x, double y, long value = 1, double vHigh = 5.0, double vLow = 0.0, int bitWidth = 1)
    {
        AddComponent("constant", name, x, y, value);
        if (vHigh != 5.0) SetProperty(name, "VoltageHigh", vHigh);
        if (vLow != 0.0) SetProperty(name, "VoltageLow", vLow);
        if (bitWidth != 1) SetProperty(name, "BitWidth", bitWidth);
    }

    public void AddDoNotConnect(string name, double x, double y, int bitWidth = 1)
    {
        AddComponent("dnc", name, x, y, 0);
        if (bitWidth != 1) SetProperty(name, "BitWidth", bitWidth);
    }

    public void AddLossyTransmissionLine(string name, double x, double y, double r = 0.2, double l = 250e-9, double g = 10e-6, double c = 100e-12, double length = 1.0)
    {
        AddComponent("lossytransmissionline", name, x, y, length);
        SetProperty(name, "R", r);
        SetProperty(name, "L", l);
        SetProperty(name, "G", g);
        SetProperty(name, "C", c);
    }

    public void AddCoaxialCable(string name, double x, double y, double z0 = 50.0, double length = 1.0, double vf = 0.66, double attenuation = 0.05)
    {
        AddComponent("coaxialcable", name, x, y, z0);
        SetProperty(name, "Length", length);
        SetProperty(name, "VF", vf);
        if (attenuation != 0.05) SetProperty(name, "Attenuation", attenuation);
    }

    public void AddMicrostripLine(string name, double x, double y, double w = 1.5e-3, double h = 0.8e-3, double er = 4.4, double length = 0.05)
    {
        AddComponent("microstripline", name, x, y, w);
        SetProperty(name, "H", h);
        SetProperty(name, "Er", er);
        SetProperty(name, "Length", length);
    }

    public void AddGround(string name, double x, double y)
    {
        AddComponent("ground", name, x, y);
    }

    public void AddBulb(string name, double x, double y, double resistance = 50.0, double ratedVoltage = 6.0, double powerRating = 0.72)
    {
        AddComponent("bulb", name, x, y, resistance);
        if (ratedVoltage != 6.0) SetProperty(name, "RatedVoltage", ratedVoltage);
        if (powerRating != 0.72) SetProperty(name, "PowerRating", powerRating);
    }

    public void AddLED(string name, double x, double y, string color = "Red", double vf = 2.0)
    {
        AddComponent("led", name, x, y, vf);
        if (!string.Equals(color, "Red", StringComparison.OrdinalIgnoreCase))
            SetCustomProperty(name, "Color", color);
    }

    public void AddLogicInput(string name, double x, double y, bool state = false, double highVoltage = 5.0, double lowVoltage = 0.0)
    {
        AddComponent("logicinput", name, x, y, state ? 1.0 : 0.0);
        if (highVoltage != 5.0) SetProperty(name, "HighVoltage", highVoltage);
        if (lowVoltage != 0.0) SetProperty(name, "LowVoltage", lowVoltage);
    }

    public void AddLogicOutput(string name, double x, double y, double threshold = 2.5, double highValue = 5.0, double lowValue = 0.0)
    {
        AddComponent("logicoutput", name, x, y, threshold);
        if (highValue != 5.0) SetProperty(name, "HighValue", highValue);
        if (lowValue != 0.0) SetProperty(name, "LowValue", lowValue);
    }

    public void AddWire(string name, double x, double y, double resistance = 0.0, double inductance = 0.0, double capacitance = 0.0)
    {
        AddComponent("wire", name, x, y, resistance);
        if (inductance != 0.0) SetProperty(name, "Inductance", inductance);
        if (capacitance != 0.0) SetProperty(name, "Capacitance", capacitance);
    }

    public void AddJunction(string name, double x, double y)
    {
        AddComponent("junction", name, x, y);
    }

    public void AddLabel(string name, double x, double y, string text = "NET1")
    {
        AddComponent("label", name, x, y);
        SetCustomProperty(name, "NetName", text);
    }

    public void AddTextAnnotation(string name, double x, double y, string text = "Note")
    {
        AddComponent("textannotation", name, x, y);
        SetCustomProperty(name, "Text", text);
    }

    // Waveform Configuration Methods
    public void ConfigureSineWaveform(string sourceName, double offset, double amplitude, double frequency, double delay = 0.0, double theta = 0.0, double phase = 0.0)
    {
        SetCustomProperty(sourceName, "WaveformType", "Sine");
        SetProperty(sourceName, "SineOffset", offset);
        SetProperty(sourceName, "SineAmplitude", amplitude);
        SetProperty(sourceName, "SineFrequency", frequency);
        SetProperty(sourceName, "SineDelay", delay);
        SetProperty(sourceName, "SineTheta", theta);
        SetProperty(sourceName, "SinePhase", phase);
    }

    public void ConfigurePulseWaveform(string sourceName, double initial, double pulsed, double delay, double riseTime, double fallTime, double width, double period)
    {
        SetCustomProperty(sourceName, "WaveformType", "Pulse");
        SetProperty(sourceName, "PulseInitial", initial);
        SetProperty(sourceName, "PulsePulsed", pulsed);
        SetProperty(sourceName, "PulseDelay", delay);
        SetProperty(sourceName, "PulseRiseTime", riseTime);
        SetProperty(sourceName, "PulseFallTime", fallTime);
        SetProperty(sourceName, "PulseWidth", width);
        SetProperty(sourceName, "PulsePeriod", period);
    }

    public void ConfigurePwlWaveform(string sourceName, string points)
    {
        SetCustomProperty(sourceName, "WaveformType", "Pwl");
        SetCustomProperty(sourceName, "PwlPoints", points);
    }

    public void ConfigureAmWaveform(string sourceName, double amplitude, double offset, double modFreq, double carrierFreq, double delay = 0.0)
    {
        SetCustomProperty(sourceName, "WaveformType", "Am");
        SetProperty(sourceName, "AmAmplitude", amplitude);
        SetProperty(sourceName, "AmOffset", offset);
        SetProperty(sourceName, "AmModFreq", modFreq);
        SetProperty(sourceName, "AmCarrierFreq", carrierFreq);
    }

    public void ConfigureSffmWaveform(string sourceName, double offset, double amplitude, double carrierFreq, double modIndex, double signalFreq)
    {
        SetCustomProperty(sourceName, "WaveformType", "Sffm");
        SetProperty(sourceName, "SffmOffset", offset);
        SetProperty(sourceName, "SffmAmplitude", amplitude);
        SetProperty(sourceName, "SffmCarrierFreq", carrierFreq);
        SetProperty(sourceName, "SffmModIndex", modIndex);
        SetProperty(sourceName, "SffmSignalFreq", signalFreq);
    }

    public void AddResistorAtGrid(string name, double resistance = 1000.0, int row = 1, int col = 1)
    {
        AddComponentAtGrid("resistor", name, row, col, resistance);
    }

    public void AddCapacitorAtGrid(string name, double capacitance = 1e-6, int row = 1, int col = 1)
    {
        AddComponentAtGrid("capacitor", name, row, col, capacitance);
    }

    public void AddInductorAtGrid(string name, double inductance = 1e-3, int row = 1, int col = 1)
    {
        AddComponentAtGrid("inductor", name, row, col, inductance);
    }

    public void AddGroundAtGrid(string name = "GND", int row = 1, int col = 1)
    {
        AddComponentAtGrid("ground", name, row, col, 0);
    }

    public void AddVoltageSourceAtGrid(string name, double dc = 5.0, int row = 1, int col = 1)
    {
        AddComponentAtGrid("voltagesource", name, row, col, dc);
    }

    public void AddCurrentSourceAtGrid(string name, double dc = 0.001, int row = 1, int col = 1)
    {
        AddComponentAtGrid("currentsource", name, row, col, dc);
    }

    public void AddNode(string name, double x = 0, double y = 0)
    {
        AddComponent("general.node", name, x, y, 0);
    }

    public void AddNodeAtGrid(string name, int row = 1, int col = 1)
    {
        AddComponentAtGrid("general.node", name, row, col, 0);
    }

    public void AddWire(string name, double x = 0, double y = 0, string direction = "horizontal")
    {
        AddComponent("general.wire", name, x, y, 0);
        SetComponentProperty(name, "Direction", direction);
    }

    public void AddWireAtGrid(string name, string direction = "horizontal", int row = 1, int col = 1)
    {
        AddComponentAtGrid("general.wire", name, row, col, 0);
        SetComponentProperty(name, "Direction", direction);
    }

    public void AddMicrophone(string name, string? audioFile = null, double bandwidth = 20000.0, double amplitude = 1.0, double x = 0, double y = 0)
    {
        AddComponent("electrical.microphone", name, x, y, 0);
        SetProperty(name, "SignalBandwidth", bandwidth);
        SetProperty(name, "Amplitude", amplitude);
        if (!string.IsNullOrWhiteSpace(audioFile))
            SetCustomProperty(name, "AudioFilePath", audioFile);
    }

    public void AddMicrophoneAtGrid(string name, string? audioFile = null, double bandwidth = 20000.0, double amplitude = 1.0, int row = 1, int col = 1)
    {
        AddComponentAtGrid("electrical.microphone", name, row, col, 0);
        SetProperty(name, "SignalBandwidth", bandwidth);
        SetProperty(name, "Amplitude", amplitude);
        if (!string.IsNullOrWhiteSpace(audioFile))
            SetCustomProperty(name, "AudioFilePath", audioFile);
    }

    public void Connect(string fromComponent, string fromPin, string toComponent, string toPin, double resistance = 0.0, IReadOnlyList<Point2D>? waypoints = null)
    {
        var cmd = new ConnectNodesCommand(_circuit, fromComponent, fromPin, toComponent, toPin, routingPoints: waypoints)
        {
            Origin = CommandOrigin.Python
        };
        _commandManager.ExecuteCommand(cmd);
        if (resistance > 0 && cmd.CreatedConnection != null)
        {
            cmd.CreatedConnection.Resistance = resistance;
        }
    }

    public void SetPosition(string componentName, double x, double y)
    {
        var cmd = new MoveComponentCommand(_circuit, componentName, x, y)
        {
            Origin = CommandOrigin.Python
        };
        _commandManager.ExecuteCommand(cmd);
    }

    public void SetLogicalGridPosition(string componentName, int row, int column)
    {
        if (row < 1) throw new ArgumentOutOfRangeException(nameof(row), "Rows are 1-based positive integers.");
        if (column < 1) throw new ArgumentOutOfRangeException(nameof(column), "Columns are 1-based positive integers.");
        SetComponentProperty(componentName, nameof(ComponentNode.LogicalRow), row);
        SetComponentProperty(componentName, nameof(ComponentNode.LogicalColumn), column);
    }

    public void SetLogicalGridSpacing(double rowSpacing, double columnSpacing)
    {
        var command = new SetLogicalGridSpacingCommand(_circuit, rowSpacing, columnSpacing)
        {
            Origin = CommandOrigin.Python
        };
        _commandManager.ExecuteCommand(command);
    }

    public LogicalGridLayoutResult ApplyLogicalGridLayout()
    {
        LogicalGridLayoutResult layout = LogicalGridSchematicLayout.Calculate(_circuit);
        foreach (LogicalGridPlacement placement in layout.Placements)
        {
            if (Math.Abs(placement.Component.X - placement.X) < 0.01 &&
                Math.Abs(placement.Component.Y - placement.Y) < 0.01)
                continue;
            SetPosition(placement.Component.Name, placement.X, placement.Y);
        }
        return layout;
    }

    public void SetProperty(string componentName, string propertyName, double value)
    {
        var cmd = new UpdatePropertyCommand(_circuit, componentName, propertyName, value)
        {
            Origin = CommandOrigin.Python
        };
        _commandManager.ExecuteCommand(cmd);
    }

    public void SetComponentProperty(string componentName, string propertyName, object? value)
    {
        var component = _circuit.FindChild<ComponentNode>(componentName)
            ?? throw new KeyNotFoundException($"Component '{componentName}' was not found.");
        var property = component.GetType().GetProperty(
            propertyName,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase);

        if (property?.SetMethod?.IsPublic == true &&
            property.GetIndexParameters().Length == 0 &&
            propertyName is not nameof(ComponentNode.Position) and
                not nameof(ComponentNode.X) and not nameof(ComponentNode.Y) and
                not nameof(ComponentNode.Rotation) and not nameof(ComponentNode.ScaleX) and
                not nameof(ComponentNode.ScaleY) and not nameof(ComponentNode.CustomProperties) and
                not nameof(ComponentNode.ElectricalParameters))
        {
            var command = new SetComponentPropertyCommand(_circuit, componentName, propertyName, value)
            {
                Origin = CommandOrigin.Python
            };
            _commandManager.ExecuteCommand(command);
            return;
        }

        if (component.ElectricalParameters.ContainsKey(propertyName) &&
            value != null && double.TryParse(
                Convert.ToString(value, CultureInfo.InvariantCulture),
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out double numericValue))
        {
            SetProperty(componentName, propertyName, numericValue);
            return;
        }

        if (component.CustomProperties.ContainsKey(propertyName) && value != null)
        {
            SetCustomProperty(componentName, propertyName, value);
            return;
        }

        throw new InvalidOperationException($"'{propertyName}' is not an editable property on {component.ComponentType}.");
    }

    public void SetMemoryWord(string componentName, int address, long value)
    {
        if (address < 0)
            throw new ArgumentOutOfRangeException(nameof(address), "Memory addresses must be non-negative.");

        var component = _circuit.FindChild<ComponentNode>(componentName);
        switch (component)
        {
            case EESimulator.Core.Components.Electronics.RAMComponent ram:
                ram.MemoryData[address] = value;
                ram.NotifyModified(nameof(ram.MemoryData));
                break;
            case EESimulator.Core.Components.Electronics.ROMComponent rom:
                rom.MemoryData[address] = value;
                rom.NotifyModified(nameof(rom.MemoryData));
                break;
            case null:
                throw new KeyNotFoundException($"Component '{componentName}' was not found.");
            default:
                throw new ArgumentException($"Component '{componentName}' is not RAM or ROM.", nameof(componentName));
        }
    }

    public void PressButton(string componentName, bool pressed = true)
    {
        var button = RequireInputComponent<EESimulator.Core.Components.Electronics.ButtonComponent>(
            componentName, "button");
        button.IsPressed = pressed;
    }

    public void SetDipSwitch(string componentName, long value)
    {
        var dip = RequireInputComponent<EESimulator.Core.Components.Electronics.DipSwitchComponent>(
            componentName, "DIP switch");
        dip.Value = value & ((1L << dip.PoleCount) - 1);
    }

    public void SetJoystick(string componentName, int x, int y, bool pressed = false)
    {
        var joystick = RequireInputComponent<EESimulator.Core.Components.Electronics.JoystickComponent>(
            componentName, "joystick");
        joystick.XValue = x;
        joystick.YValue = y;
        joystick.Pressed = pressed;
        joystick.NotifyModified(nameof(joystick.XValue));
        joystick.NotifyModified(nameof(joystick.YValue));
        joystick.NotifyModified(nameof(joystick.Pressed));
    }

    public void SendKeyboardKey(string componentName, int characterCode)
    {
        var keyboard = RequireInputComponent<EESimulator.Core.Components.Electronics.KeyboardComponent>(
            componentName, "keyboard");
        keyboard.Data = Math.Clamp(characterCode, 0, 255);
        keyboard.DataReady = true;
        keyboard.NotifyModified(nameof(keyboard.Data));
        keyboard.NotifyModified(nameof(keyboard.DataReady));
    }

    public void SetDipSwitchPole(string componentName, int poleIndex, bool state)
    {
        var dip = RequireInputComponent<DipSwitchComponent>(componentName, "DIP switch");
        if (poleIndex < 0 || poleIndex >= dip.PoleCount)
            throw new ArgumentOutOfRangeException(nameof(poleIndex), $"Pole index must be between 0 and {dip.PoleCount - 1}.");
        long mask = 1L << poleIndex;
        dip.Value = state ? (dip.Value | mask) : (dip.Value & ~mask);
        dip.NotifyModified(nameof(dip.Value));
    }

    public void SetLogicBus(IEnumerable<string> componentNames, long value)
    {
        int bit = 0;
        foreach (var name in componentNames)
        {
            bool bitState = ((value >> bit) & 1) == 1;
            SetLogic(name, bitState);
            bit++;
        }
    }

    public void SetConstant(string componentName, long value, int? bitWidth = null, double? highVoltage = null, double? lowVoltage = null)
    {
        var cst = RequireInputComponent<ConstantComponent>(componentName, "Constant");
        cst.Value = value;
        if (bitWidth.HasValue) cst.BitWidth = bitWidth.Value;
        if (highVoltage.HasValue) cst.VoltageHigh = highVoltage.Value;
        if (lowVoltage.HasValue) cst.VoltageLow = lowVoltage.Value;
        cst.NotifyModified(nameof(cst.Value));
    }

    public void SetVoltageSwitch(string componentName, double? threshold = null, double? hysteresis = null, double? ron = null, double? roff = null)
    {
        var sw = RequireInputComponent<VoltageSwitchComponent>(componentName, "Voltage Switch");
        if (threshold.HasValue) sw.Threshold = threshold.Value;
        if (hysteresis.HasValue) sw.Hysteresis = hysteresis.Value;
        if (ron.HasValue) sw.ResistanceOn = ron.Value;
        if (roff.HasValue) sw.ResistanceOff = roff.Value;
        sw.NotifyModified(nameof(sw.Threshold));
    }

    public void SetCurrentSwitch(string componentName, double? threshold = null, double? hysteresis = null, double? ron = null, double? roff = null, string? controllingSource = null)
    {
        var sw = RequireInputComponent<CurrentSwitchComponent>(componentName, "Current Switch");
        if (threshold.HasValue) sw.Threshold = threshold.Value;
        if (hysteresis.HasValue) sw.Hysteresis = hysteresis.Value;
        if (ron.HasValue) sw.ResistanceOn = ron.Value;
        if (roff.HasValue) sw.ResistanceOff = roff.Value;
        if (controllingSource != null) sw.ControllingSource = controllingSource;
        sw.NotifyModified(nameof(sw.Threshold));
    }

    public void SetRelay(string componentName, double? pullInVoltage = null, double? dropoutVoltage = null, double? coilResistance = null, double? contactResistance = null, double? openResistance = null)
    {
        var relay = RequireInputComponent<RelayComponent>(componentName, "Relay");
        if (pullInVoltage.HasValue) relay.PullInVoltage = pullInVoltage.Value;
        if (dropoutVoltage.HasValue) relay.DropoutVoltage = dropoutVoltage.Value;
        if (coilResistance.HasValue) relay.CoilResistance = coilResistance.Value;
        if (contactResistance.HasValue) relay.ContactResistance = contactResistance.Value;
        if (openResistance.HasValue) relay.OpenResistance = openResistance.Value;
        relay.ValidateConfiguration();
        relay.NotifyModified(nameof(relay.PullInVoltage));
    }

    public void SetMicrophoneAudio(string componentName, string audioFilePath, double? amplitude = null, double? bandwidth = null, double? offset = null)
    {
        var mic = RequireInputComponent<MicrophoneComponent>(componentName, "Microphone");
        if (amplitude.HasValue) mic.Amplitude = amplitude.Value;
        if (bandwidth.HasValue) mic.SignalBandwidth = bandwidth.Value;
        if (offset.HasValue) mic.Offset = offset.Value;
        if (!string.IsNullOrEmpty(audioFilePath)) mic.AudioFilePath = audioFilePath;
        mic.NotifyModified(nameof(mic.AudioFilePath));
    }

    // ── Signal Generation (Voltage Source, AC Source, Clock) ────────────────
    public void SetDcSignal(string componentName, double voltage)
    {
        var vs = RequireInputComponent<VoltageSourceComponent>(componentName, "Voltage Source");
        vs.WaveformType = "DC";
        vs.Voltage = voltage;
        vs.NotifyModified(nameof(vs.Voltage));
        vs.NotifyModified(nameof(vs.WaveformType));
    }

    public void SetSineSignal(string componentName, double amplitude, double frequency, double offset = 0.0, double phase = 0.0, double delay = 0.0, double theta = 0.0)
    {
        var vs = RequireInputComponent<VoltageSourceComponent>(componentName, "Voltage Source");
        vs.WaveformType = "Sine";
        vs.SineAmplitude = amplitude;
        vs.SineFrequency = frequency;
        vs.SineOffset = offset;
        vs.SinePhase = phase;
        vs.SineDelay = delay;
        vs.SineTheta = theta;
        vs.NotifyModified(nameof(vs.SineAmplitude));
        vs.NotifyModified(nameof(vs.WaveformType));
    }

    public void SetPulseSignal(string componentName, double initial, double pulsed, double period, double width, double delay = 0.0, double riseTime = 1e-6, double fallTime = 1e-6)
    {
        var vs = RequireInputComponent<VoltageSourceComponent>(componentName, "Voltage Source");
        vs.WaveformType = "Pulse";
        vs.PulseInitial = initial;
        vs.PulsePulsed = pulsed;
        vs.PulsePeriod = period;
        vs.PulseWidth = width;
        vs.PulseDelay = delay;
        vs.PulseRiseTime = riseTime;
        vs.PulseFallTime = fallTime;
        vs.NotifyModified(nameof(vs.PulsePulsed));
        vs.NotifyModified(nameof(vs.WaveformType));
    }

    public void SetStepSignal(string componentName, double initial, double stepValue, double delay = 0.0)
    {
        SetPulseSignal(componentName, initial, stepValue, period: 1000.0, width: 999.0, delay: delay, riseTime: 1e-9, fallTime: 1e-9);
    }

    public void SetRampSignal(string componentName, double startVoltage, double peakVoltage, double duration, double delay = 0.0, double? period = null)
    {
        double rampPeriod = period ?? (duration * 2);
        SetPulseSignal(componentName, initial: startVoltage, pulsed: peakVoltage, period: rampPeriod, width: 0.0, delay: delay, riseTime: duration, fallTime: 1e-9);
    }

    public void SetTriangleSignal(string componentName, double lowVoltage, double highVoltage, double period, double delay = 0.0)
    {
        double half = period / 2.0;
        SetPulseSignal(componentName, initial: lowVoltage, pulsed: highVoltage, period: period, width: 0.0, delay: delay, riseTime: half, fallTime: half);
    }

    public void SetPwlSignal(string componentName, string pwlPoints)
    {
        var vs = RequireInputComponent<VoltageSourceComponent>(componentName, "Voltage Source");
        vs.WaveformType = "Pwl";
        vs.PwlPoints = pwlPoints;
        vs.NotifyModified(nameof(vs.PwlPoints));
        vs.NotifyModified(nameof(vs.WaveformType));
    }

    public void SetAcSource(string componentName, double voltageRms, double frequency, string waveform = "Sine", double offset = 0.0, double phase = 0.0)
    {
        var ac = RequireInputComponent<ACSourceComponent>(componentName, "AC Source");
        ac.WaveformType = waveform;
        ac.Voltage = voltageRms;
        ac.Frequency = frequency;
        ac.Offset = offset;
        ac.PhaseDegrees = phase;
        ac.NotifyModified(nameof(ac.Voltage));
        ac.NotifyModified(nameof(ac.WaveformType));
    }

    public void SetClockSignal(string componentName, double frequency, double dutyCycle = 0.5, double highVoltage = 5.0, double lowVoltage = 0.0)
    {
        var clk = RequireInputComponent<ClockComponent>(componentName, "Clock");
        clk.Frequency = frequency;
        clk.DutyCycle = dutyCycle;
        clk.HighVoltage = highVoltage;
        clk.LowVoltage = lowVoltage;
        clk.NotifyModified(nameof(clk.Frequency));
    }

    private T RequireInputComponent<T>(string componentName, string inputName)
        where T : ComponentNode
    {
        var component = _circuit.FindChild<ComponentNode>(componentName)
            ?? throw new KeyNotFoundException($"Component '{componentName}' was not found.");
        return component as T
            ?? throw new ArgumentException(
                $"Component '{componentName}' is not a {inputName}.", nameof(componentName));
    }

    public void SetCustomProperty(string componentName, string propertyName, object value)
    {
        var comp = _circuit.FindChild<ComponentNode>(componentName);
        if (comp != null)
        {
            var property = comp.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase);
            if (property?.CanWrite == true && property.GetIndexParameters().Length == 0)
            {
                Type targetType = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
                object converted = targetType.IsEnum
                    ? value is string enumText ? Enum.Parse(targetType, enumText, true) : Enum.ToObject(targetType, value)
                    : targetType.IsInstanceOfType(value) ? value
                    : Convert.ChangeType(value, targetType, CultureInfo.InvariantCulture);
                property.SetValue(comp, converted);
            }
            else
            {
                comp.CustomProperties[propertyName] = value;
            }
            comp.NotifyModified(propertyName);
        }
    }

    // Live Probe & Waveform State Automation
    public void SetLiveProbe(string componentName, bool enabled)
    {
        var comp = _circuit.FindChild<ComponentNode>(componentName);
        if (comp != null)
        {
            comp.ShowLiveProbe = enabled;
            comp.NotifyModified(nameof(comp.ShowLiveProbe));
        }
    }

    public void SetWaveform(string componentName, bool enabled)
    {
        var comp = _circuit.FindChild<ComponentNode>(componentName);
        if (comp != null)
        {
            comp.ShowWaveform = enabled;
            comp.NotifyModified(nameof(comp.ShowWaveform));
        }
    }

    // Terminal Pin Renaming Automation
    public void RenamePin(string componentName, string pinIdOrName, string newName)
    {
        var comp = _circuit.FindChild<ComponentNode>(componentName);
        if (comp != null)
        {
            var pin = comp.GetPin(pinIdOrName);
            if (pin != null)
            {
                pin.Name = newName;
                comp.NotifyModified(nameof(comp.Pins));
            }
        }
    }

    // Wire Parasitic Resistance Automation
    public void SetWireResistance(string wireIdOrName, double resistance)
    {
        var conn = _circuit.FindChildren<ConnectionNode>().FirstOrDefault(c =>
            c.Name.Equals(wireIdOrName, StringComparison.OrdinalIgnoreCase) ||
            c.Id.ToString().Equals(wireIdOrName, StringComparison.OrdinalIgnoreCase) ||
            c.NetName.Equals(wireIdOrName, StringComparison.OrdinalIgnoreCase));
        if (conn != null)
        {
            conn.Resistance = resistance;
            conn.NotifyModified(nameof(conn.Resistance));
            return;
        }

        var wireComp = _circuit.FindChild<EESimulator.Core.Components.General.WireComponent>(wireIdOrName);
        if (wireComp != null)
        {
            wireComp.Resistance = resistance;
            wireComp.NotifyModified("Resistance");
        }
    }

    // Control Sidebar Quick Button Registration Automation
    public void RegisterControlButton(string label, string actionName)
    {
        OnControlButtonRegistered?.Invoke(label, actionName);
    }

    // Interactive Viewport Active Tool Selection Automation
    public void SetActiveTool(string toolName)
    {
        OnActiveToolChanged?.Invoke(toolName);
    }

    // Simulation Operations
    public SimulationResultsData Simulate(
        string analysisType = "OperatingPoint",
        double? stopTime = null,
        double? stepTime = null)
    {
        var settings = new SimulationSettings
        {
            Analysis = analysisType.ToLowerInvariant() switch
            {
                "transient" => AnalysisType.Transient,
                "ac" => AnalysisType.AC,
                "dcsweep" => AnalysisType.DCSweep,
                _ => AnalysisType.OperatingPoint
            }
        };

        if (stopTime is double stop)
        {
            if (!double.IsFinite(stop) || stop <= 0)
                throw new ArgumentOutOfRangeException(nameof(stopTime), "Stop time must be finite and positive.");
            settings.StopTime = stop;
        }

        if (stepTime is double step)
        {
            if (!double.IsFinite(step) || step <= 0)
                throw new ArgumentOutOfRangeException(nameof(stepTime), "Step time must be finite and positive.");
            settings.StepTime = step;
        }

        return _simulationManager.Run(_circuit, settings);
    }

    public void EvaluateCombinational()
    {
        DigitalLogicSimulator.EvaluateCombinational(_circuit);
    }

    public void AdvanceSimulation(double elapsedSeconds)
    {
        if (!double.IsFinite(elapsedSeconds) || elapsedSeconds < 0)
            throw new ArgumentOutOfRangeException(nameof(elapsedSeconds), "Elapsed simulation time must be finite and non-negative.");

        DigitalLogicSimulator.Advance(_circuit, elapsedSeconds);
    }

    public bool ReadDigitalOutputState(string componentName, string pinId)
    {
        var component = _circuit.FindChild<ComponentNode>(componentName)
            ?? throw new KeyNotFoundException($"Component '{componentName}' was not found.");
        return DigitalLogicSimulator.ReadOutputState(component, pinId);
    }

    public double ReadPinVoltage(string componentName, string pinId)
    {
        var component = _circuit.FindChild<ComponentNode>(componentName)
            ?? throw new KeyNotFoundException($"Component '{componentName}' was not found.");
        if (!component.LivePinVoltages.TryGetValue(pinId, out double voltage))
            throw new KeyNotFoundException($"Pin '{pinId}' on component '{componentName}' has no measured voltage.");
        return voltage;
    }

    // Application & Viewport Operations
    public void OpenWindow(string windowName)
    {
        OnWindowOpenRequested?.Invoke(windowName);
    }

    public void SetActiveViewport(string viewportName)
    {
        OnActiveViewportChanged?.Invoke(viewportName);
    }

    // Output & Logic Component Helpers
    public void ToggleLogic(string componentName)
    {
        var comp = _circuit.FindChild<ComponentNode>(componentName);
        if (comp is EESimulator.Core.Components.Electronics.LogicInputComponent lin)
        {
            lin.Toggle();
        }
        else if (comp != null)
        {
            bool cur = comp.CustomProperties.TryGetValue("State", out var v) && Convert.ToBoolean(v);
            comp.CustomProperties["State"] = !cur;
            comp.ElectricalParameters["State"] = !cur ? 1.0 : 0.0;
            comp.NotifyModified("State");
        }
        OnLogicToggled?.Invoke(componentName);
    }

    public void SetLogic(string componentName, bool state)
    {
        var comp = _circuit.FindChild<ComponentNode>(componentName);
        if (comp is EESimulator.Core.Components.Electronics.LogicInputComponent lin)
        {
            lin.State = state;
        }
        else if (comp != null)
        {
            comp.CustomProperties["State"] = state;
            comp.ElectricalParameters["State"] = state ? 1.0 : 0.0;
            comp.NotifyModified("State");
        }
        OnLogicToggled?.Invoke(componentName);
    }

    public void SetLogicLevels(string componentName, double highVoltage, double lowVoltage)
    {
        SetProperty(componentName, "HighVoltage", highVoltage);
        SetProperty(componentName, "LowVoltage", lowVoltage);
    }

    public void SetOutputState(string componentName, bool isOn)
    {
        var comp = _circuit.FindChild<ComponentNode>(componentName);
        if (comp != null)
        {
            comp.CustomProperties["IsOn"] = isOn;
            comp.ElectricalParameters["IsOn"] = isOn ? 1.0 : 0.0;
            comp.NotifyModified("IsOn");
        }
    }

    // Component Transformations: Rotate & Stretch
    public void RotateComponent(string componentName, double angleDegrees)
    {
        var comp = _circuit.FindChild<ComponentNode>(componentName);
        if (comp != null)
        {
            comp.Rotation = (comp.Rotation + angleDegrees) % 360.0;
            comp.NotifyModified(nameof(comp.Rotation));
        }
        OnComponentRotateRequested?.Invoke(componentName, angleDegrees);
    }

    public void SetRotation(string componentName, double angleDegrees)
    {
        var comp = _circuit.FindChild<ComponentNode>(componentName);
        if (comp != null)
        {
            comp.Rotation = angleDegrees % 360.0;
            comp.NotifyModified(nameof(comp.Rotation));
        }
        OnComponentRotateRequested?.Invoke(componentName, angleDegrees);
    }

    public void StretchComponent(string componentName, double scaleX, double scaleY)
    {
        var comp = _circuit.FindChild<ComponentNode>(componentName);
        if (comp != null)
        {
            comp.ScaleX = Math.Clamp(scaleX, 0.2, 10.0);
            comp.ScaleY = Math.Clamp(scaleY, 0.2, 10.0);
            comp.NotifyModified(nameof(comp.ScaleX));
            comp.NotifyModified(nameof(comp.ScaleY));
        }
        OnComponentStretchRequested?.Invoke(componentName, scaleX, scaleY);
    }

    public void SetScale(string componentName, double scaleX, double scaleY)
    {
        StretchComponent(componentName, scaleX, scaleY);
    }

    // Default Workspace & UI Controls
    public void ToggleComponentSidebar(bool? visible = null)
    {
        var sb = UI.Sidebar("ComponentSidebar") ?? UI.Sidebar("ComponentPalette");
        if (sb != null)
        {
            sb.Visible = visible ?? !sb.Visible;
        }
        OnSidebarToggled?.Invoke(visible);
    }

    public void ToggleControlSidebar(bool? visible = null)
    {
        var sb = UI.Sidebar("ControlSidebar");
        if (sb != null)
        {
            sb.Visible = visible ?? !sb.Visible;
        }
        OnControlSidebarToggled?.Invoke(visible);
    }

    public void ToggleLeftToolbar(bool? visible = null)
    {
        OnLeftToolbarToggled?.Invoke(visible);
    }

    public void SetSidebarVisible(string sidebarName, bool visible)
    {
        var sb = UI.Sidebar(sidebarName);
        if (sb != null)
        {
            sb.Visible = visible;
        }
        OnSidebarVisibilityChanged?.Invoke(sidebarName, visible);
    }

    public void PlaySimulation()
    {
        OnSimulationPlayRequested?.Invoke();
    }

    public void PauseSimulation()
    {
        OnSimulationPauseRequested?.Invoke();
    }

    public void ToggleSimulationPlayback()
    {
        OnSimulationToggleRequested?.Invoke();
    }

    public void StopSimulation()
    {
        OnSimulationStopRequested?.Invoke();
    }

    public void SetProjectName(string name)
    {
        OnProjectNameChanged?.Invoke(name);
    }

    public void Undo()
    {
        _commandManager.Undo();
        OnUndoRequested?.Invoke();
    }

    public void Redo()
    {
        _commandManager.Redo();
        OnRedoRequested?.Invoke();
    }

    public void CopyComponent(string componentName)
    {
        var comp = _circuit.FindChild<ComponentNode>(componentName);
        if (comp != null)
        {
            var clone = comp.Clone();
            clone.Name = $"{comp.Name}_copy";
            clone.Position = (comp.Position.X + 40, comp.Position.Y + 40);
            var cmd = new AddComponentCommand(_circuit, clone)
            {
                Origin = CommandOrigin.Python
            };
            _commandManager.ExecuteCommand(cmd);
            OnComponentCopied?.Invoke(componentName);
        }
    }

    public void DeleteComponent(string componentName)
    {
        var comp = _circuit.FindChild<ComponentNode>(componentName);
        if (comp != null)
        {
            var cmd = new RemoveComponentCommand(_circuit, comp)
            {
                Origin = CommandOrigin.Python
            };
            _commandManager.ExecuteCommand(cmd);
            OnComponentDeleted?.Invoke(componentName);
        }
    }

    public void DeleteConnection(string fromComponentName, string fromPin, string toComponentName, string toPin)
    {
        var fromComponent = _circuit.FindChild<ComponentNode>(fromComponentName)
            ?? throw new KeyNotFoundException($"Component '{fromComponentName}' was not found.");
        var toComponent = _circuit.FindChild<ComponentNode>(toComponentName)
            ?? throw new KeyNotFoundException($"Component '{toComponentName}' was not found.");

        var connection = _circuit.Connections.FirstOrDefault(candidate =>
            candidate.FromComponentId == fromComponent.Id &&
            candidate.FromPin.Equals(fromPin, StringComparison.OrdinalIgnoreCase) &&
            candidate.ToComponentId == toComponent.Id &&
            candidate.ToPin.Equals(toPin, StringComparison.OrdinalIgnoreCase) ||
            candidate.FromComponentId == toComponent.Id &&
            candidate.FromPin.Equals(toPin, StringComparison.OrdinalIgnoreCase) &&
            candidate.ToComponentId == fromComponent.Id &&
            candidate.ToPin.Equals(fromPin, StringComparison.OrdinalIgnoreCase));
        if (connection == null)
            throw new KeyNotFoundException(
                $"Connection '{fromComponentName}.{fromPin}' to '{toComponentName}.{toPin}' was not found.");

        var command = new RemoveConnectionCommand(_circuit, connection)
        {
            Origin = CommandOrigin.Python
        };
        if (!_commandManager.ExecuteCommand(command))
            throw new InvalidOperationException("The wire connection could not be removed.");

        OnConnectionDeleted?.Invoke();
    }

    public void SetBreakingCurrent(string componentName, double breakingCurrent)
    {
        var comp = _circuit.FindChild<ComponentNode>(componentName);
        if (comp != null)
        {
            comp.BreakingCurrent = breakingCurrent;
            comp.NotifyModified(nameof(comp.BreakingCurrent));
        }
    }

    public void SetDescription(string componentName, string description)
    {
        var comp = _circuit.FindChild<ComponentNode>(componentName);
        if (comp != null)
        {
            comp.Description = description;
            comp.NotifyModified(nameof(comp.Description));
        }
    }

    public void ToggleDescriptionSidebar(bool? visible = null)
    {
        var sb = UI.Sidebar("DescriptionSidebar");
        if (sb != null)
        {
            sb.Visible = visible ?? !sb.Visible;
        }
        OnDescriptionSidebarToggled?.Invoke();
    }

    public void SaveProject(string? filePath = null)
    {
        OnSaveRequested?.Invoke(filePath);
    }

    public void SaveProjectAs(string filePath, string format = "eee")
    {
        OnSaveAsRequested?.Invoke(filePath, format);
    }

    public void SaveCircuitImage(string? filePath = null, string view = "recentre") =>
        OnCircuitImageSaveRequested?.Invoke(filePath, view);

    public void StartVideoRecording(
        string? filePath = null,
        int frameRate = 30,
        string format = "gif",
        string view = "recentre",
        double? duration = null,
        bool requireConfirmation = false)
    {
        if (frameRate is < 1 or > 30)
            throw new ArgumentOutOfRangeException(nameof(frameRate), "Recording frame rate must be between 1 and 30 fps.");
        if (!format.Equals("gif", StringComparison.OrdinalIgnoreCase) &&
            !format.Equals("mp4", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Video format must be GIF or MP4.", nameof(format));
        VideoFrameRate = frameRate;
        VideoFormat = format.ToUpperInvariant();
        IsVideoRecording = true;
        OnVideoRecordingStartRequested?.Invoke(
            filePath,
            frameRate,
            VideoFormat,
            view.ToLowerInvariant() == "screen" ? "screen" : "recentre",
            duration,
            requireConfirmation);
    }

    public void StopVideoRecording()
    {
        IsVideoRecording = false;
        OnVideoRecordingStopRequested?.Invoke();
    }

    public void SetVideoRecordingState(bool isRecording) =>
        IsVideoRecording = isRecording;

    public void ToggleWaveformDrawer()
    {
        UI.Workspace.ToggleWaveform();
        OnWaveformDrawerToggleRequested?.Invoke();
    }

    public void SetWaveformDrawerVisible(bool visible)
    {
        OnWaveformDrawerVisibilityRequested?.Invoke(visible);
    }

    public void OpenWaveformDrawer() => SetWaveformDrawerVisible(true);
    public void CloseWaveformDrawer() => SetWaveformDrawerVisible(false);

    public void CloseSidebars() => OnCloseSidebarsRequested?.Invoke();
    public void OpenSidebars() => OnOpenSidebarsRequested?.Invoke();
    public void SetLeftSidebarVisible(bool visible) => OnLeftToolbarToggled?.Invoke(visible);
    public void SetRightSidebarVisible(bool visible) => OnSidebarToggled?.Invoke(visible);

    public void SetSubtitle(string text)
    {
        SubtitleText = text ?? string.Empty;
        OnSubtitleChanged?.Invoke(SubtitleText);
    }

    public void ClearSubtitle() => SetSubtitle(string.Empty);
    public string GetSubtitle() => SubtitleText;

    private readonly Dictionary<string, object> _workspaceSettings = new(StringComparer.OrdinalIgnoreCase)
    {
        ["AutoRunSimulation"] = false,
        ["DefaultCategory"] = "All",
        ["ViewportRefreshHz"] = 30,
        ["GraphRefreshHz"] = 20,
        ["InteractionLoopHz"] = 60,
        ["UiSnapshotDelayMs"] = 40,
        ["VoltageChangeThreshold"] = 0.01,
        ["CurrentChangeThreshold"] = 0.001,
        ["ReactiveOscillationThreshold"] = 0.02,
        ["RemoteApiBaseUrl"] = string.Empty,
        ["EnableTimeScaling"] = false,
        ["SimulationTimeAmount"] = 30.0,
        ["SimulationTimeUnit"] = "seconds",
        ["DeviceTimeAmount"] = 1.0,
        ["DeviceTimeUnit"] = "hours",
        ["SimulationTimeStepSeconds"] = 0.04,
        ["AutomaticTimeStepSelection"] = true,
        ["SimulationStartTimeSeconds"] = 0.0,
        ["SimulationDurationSeconds"] = 60.0
    };

    public void SetWorkspaceSetting(string name, object? value)
    {
        if (string.IsNullOrWhiteSpace(name)) return;
        string key = NormalizeWorkspaceSettingKey(name);
        if (value != null)
            _workspaceSettings[key] = value;
        OnWorkspaceSettingChanged?.Invoke(name, value);
    }

    public object? GetWorkspaceSetting(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return null;
        string key = NormalizeWorkspaceSettingKey(name);
        return _workspaceSettings.TryGetValue(key, out var val) ? val : null;
    }

    public void ResetWorkspaceSettings()
    {
        _workspaceSettings["AutoRunSimulation"] = false;
        _workspaceSettings["DefaultCategory"] = "All";
        _workspaceSettings["ViewportRefreshHz"] = 30;
        _workspaceSettings["GraphRefreshHz"] = 20;
        _workspaceSettings["InteractionLoopHz"] = 60;
        _workspaceSettings["UiSnapshotDelayMs"] = 40;
        _workspaceSettings["VoltageChangeThreshold"] = 0.01;
        _workspaceSettings["CurrentChangeThreshold"] = 0.001;
        _workspaceSettings["ReactiveOscillationThreshold"] = 0.02;
        _workspaceSettings["RemoteApiBaseUrl"] = string.Empty;
        _workspaceSettings["EnableTimeScaling"] = false;
        _workspaceSettings["SimulationTimeAmount"] = 30.0;
        _workspaceSettings["SimulationTimeUnit"] = "seconds";
        _workspaceSettings["DeviceTimeAmount"] = 1.0;
        _workspaceSettings["DeviceTimeUnit"] = "hours";
        _workspaceSettings["SimulationTimeStepSeconds"] = 0.04;
        _workspaceSettings["AutomaticTimeStepSelection"] = true;
        _workspaceSettings["SimulationStartTimeSeconds"] = 0.0;
        _workspaceSettings["SimulationDurationSeconds"] = 60.0;
        OnWorkspaceSettingsResetRequested?.Invoke();
    }

    private static string NormalizeWorkspaceSettingKey(string name)
    {
        string raw = new string(name.Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();
        return raw switch
        {
            "autorunsimulation" => "AutoRunSimulation",
            "defaultcategory" or "defaultpalettecategory" or "palettecategory" => "DefaultCategory",
            "viewportrefreshhz" => "ViewportRefreshHz",
            "graphrefreshhz" => "GraphRefreshHz",
            "interactionloophz" => "InteractionLoopHz",
            "uisnapshotdelayms" => "UiSnapshotDelayMs",
            "voltagedeltathreshold" or "voltagechangethreshold" => "VoltageChangeThreshold",
            "currentdeltathreshold" or "currentchangethreshold" => "CurrentChangeThreshold",
            "reactiveoscillationthreshold" => "ReactiveOscillationThreshold",
            "hcvpapibaseurl" or "remoteapibaseurl" => "RemoteApiBaseUrl",
            "enabletimescaling" or "timescalingenabled" => "EnableTimeScaling",
            "simulationtime" or "simulationtimeamount" => "SimulationTimeAmount",
            "simulationtimeunit" => "SimulationTimeUnit",
            "devicetime" or "devicetimeamount" => "DeviceTimeAmount",
            "devicetimeunit" => "DeviceTimeUnit",
            "solverstep" or "simulationtimestepseconds" => "SimulationTimeStepSeconds",
            "automatictimestepselection" => "AutomaticTimeStepSelection",
            "starttime" or "simulationstarttimeseconds" => "SimulationStartTimeSeconds",
            "duration" or "simulationdurationseconds" => "SimulationDurationSeconds",
            _ => name
        };
    }

    public void ToggleView()
    {
        OnViewToggleRequested?.Invoke();
    }

    public void AddSinglePhaseTransmissionLine(string name, double x, double y, double ratedVoltage = 230, double frequency = 50, double lengthKm = 1, double resistancePerKm = 0.2, double inductancePerKm = 1e-3, double capacitancePerKm = 10e-9, double conductancePerKm = 0, string modelType = "Short Line", string supplyMode = "ExternalGenerators", double fixedVoltageRms = 230)
    {
        AddComponent("singlephasetransmissionline", name, x, y, ratedVoltage);
        SetComponentProperty(name, "Frequency", frequency);
        SetComponentProperty(name, "LengthKm", lengthKm);
        SetComponentProperty(name, "ResistancePerKm", resistancePerKm);
        SetComponentProperty(name, "InductancePerKm", inductancePerKm);
        SetComponentProperty(name, "CapacitancePerKm", capacitancePerKm);
        SetComponentProperty(name, "ConductancePerKm", conductancePerKm);
        SetComponentProperty(name, "ModelType", modelType);
        SetComponentProperty(name, "SupplyMode", supplyMode);
        SetComponentProperty(name, "FixedVoltageRms", fixedVoltageRms);
    }

    public void AddTwoPhaseTransmissionLine(string name, double x, double y, double ratedVoltage = 240, double frequency = 60, double lengthKm = 1, double phaseDisplacement = 90, string configuration = "TrueTwoPhase", string supplyMode = "ExternalGenerators", double fixedVoltageRms = 120)
    {
        AddComponent("twophasetransmissionline", name, x, y, ratedVoltage);
        SetComponentProperty(name, "Frequency", frequency);
        SetComponentProperty(name, "LengthKm", lengthKm);
        SetComponentProperty(name, "PhaseDisplacementDegrees", phaseDisplacement);
        SetComponentProperty(name, "ConductorConfiguration", configuration);
        SetComponentProperty(name, "SupplyMode", supplyMode);
        SetComponentProperty(name, "FixedVoltageRms", fixedVoltageRms);
    }

    public void AddThreePhaseTransmissionLine(string name, double x, double y, double ratedLineVoltage = 11000, double frequency = 50, double lengthKm = 1, string phaseSequence = "ABC", string connection = "Three-wire", string modelType = "Short", string supplyMode = "ExternalGenerators", double fixedPhaseVoltageRms = 6350.85)
    {
        AddComponent("threephasetransmissionline", name, x, y, ratedLineVoltage);
        SetComponentProperty(name, "Frequency", frequency);
        SetComponentProperty(name, "LengthKm", lengthKm);
        SetComponentProperty(name, "PhaseSequence", phaseSequence);
        SetComponentProperty(name, "Connection", connection);
        SetComponentProperty(name, "ModelType", modelType);
        SetComponentProperty(name, "SupplyMode", supplyMode);
        SetComponentProperty(name, "FixedPhaseVoltageRms", fixedPhaseVoltageRms);
    }

    public void AddACTransformer(string name, double x, double y, double primaryVoltage = 230, double secondaryVoltage = 115, double apparentPower = 1000, double frequency = 50, string connection = "Single-phase")
    {
        AddComponent("actransformer", name, x, y, primaryVoltage);
        SetComponentProperty(name, "RatedSecondaryVoltage", secondaryVoltage);
        SetComponentProperty(name, "RatedApparentPower", apparentPower);
        SetComponentProperty(name, "Frequency", frequency);
        SetComponentProperty(name, "WindingConnection", connection);
    }

    public void AddSynchronousGenerator(string name, double x, double y, double ratedVoltage = 400, double ratedFrequency = 50, double ratedPower = 10000, int phases = 3, int poles = 4)
    {
        AddComponent("synchronousgenerator", name, x, y, ratedVoltage);
        SetComponentProperty(name, "RatedFrequency", ratedFrequency);
        SetComponentProperty(name, "RatedApparentPower", ratedPower);
        SetComponentProperty(name, "NumberOfPhases", phases);
        SetComponentProperty(name, "NumberOfPoles", poles);
    }

    public void AddInductionMotor(string name, double x, double y, double ratedVoltage = 400, double ratedPower = 1500, double frequency = 50, int poles = 4)
    {
        AddComponent("inductionmotor", name, x, y, ratedVoltage);
        SetComponentProperty(name, "RatedPower", ratedPower);
        SetComponentProperty(name, "Frequency", frequency);
        SetComponentProperty(name, "NumberOfPoles", poles);
    }

    public void AddACBulb(string name, double x, double y, double ratedVoltage = 230, double ratedPower = 60, bool initiallyOn = true, double dimmingPercent = 100)
    {
        AddComponent("acbulb", name, x, y, ratedVoltage);
        SetComponentProperty(name, "RatedPower", ratedPower);
        SetComponentProperty(name, "InitiallyOn", initiallyOn);
        SetComponentProperty(name, "DimmingPercent", dimmingPercent);
    }

    public void AddACFan(string name, double x, double y, double ratedVoltage = 230, double ratedPower = 75, double powerFactor = 0.8, int selectedSpeed = 3)
    {
        AddComponent("acfan", name, x, y, ratedVoltage);
        SetComponentProperty(name, "RatedPower", ratedPower);
        SetComponentProperty(name, "PowerFactor", powerFactor);
        SetComponentProperty(name, "SelectedSpeed", selectedSpeed);
    }

    public void AddSolarPanel(string name, double x, double y, double maximumPowerRating = 300, double openCircuitVoltage = 40, double shortCircuitCurrent = 9, double irradiance = 1000)
    {
        AddComponent("solarpanel", name, x, y, maximumPowerRating);
        SetComponentProperty(name, "OpenCircuitVoltage", openCircuitVoltage);
        SetComponentProperty(name, "ShortCircuitCurrent", shortCircuitCurrent);
        SetComponentProperty(name, "SolarIrradiance", irradiance);
    }

    public void AddBattery(string name, double x, double y, double nominalVoltage = 12, double capacityAh = 100, double initialSocPercent = 80, string chemistry = "Lithium-ion")
    {
        AddComponent("battery", name, x, y, nominalVoltage);
        SetComponentProperty(name, "CapacityAh", capacityAh);
        SetComponentProperty(name, "InitialSocPercent", initialSocPercent);
        SetComponentProperty(name, "StateOfChargePercent", initialSocPercent);
        SetComponentProperty(name, "Chemistry", chemistry);
    }
}
