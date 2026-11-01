using System;
using System.Collections.Generic;
using EESimulator.Commands;
using EESimulator.Commands.Commands;
using EESimulator.Core.Components;
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

    public UIRootNode UI { get; }

    public event Action<string>? OnWindowOpenRequested;
    public event Action<string>? OnActiveViewportChanged;
    public event Action<string, string>? OnControlButtonRegistered;
    public event Action<string>? OnActiveToolChanged;
    public event Action<bool?>? OnSidebarToggled;
    public event Action<bool?>? OnControlSidebarToggled;
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
    public event Action<string>? OnComponentCopied;
    public event Action<string?>? OnSaveRequested;
    public event Action<string, string>? OnSaveAsRequested;

    public PythonApi(CommandManager commandManager, CircuitNode circuit, SimulationManager? simulationManager = null, UIRootNode? uiRoot = null)
    {
        _commandManager = commandManager ?? throw new ArgumentNullException(nameof(commandManager));
        _circuit = circuit ?? throw new ArgumentNullException(nameof(circuit));
        _simulationManager = simulationManager ?? new SimulationManager();
        UI = uiRoot ?? new UIRootNode();
    }

    // Circuit Operations
    public void AddComponent(string type, string name, double x, double y, double param = 0)
    {
        var cmd = new AddComponentCommand(_circuit, type, name, x, y, param)
        {
            Origin = CommandOrigin.Python
        };
        _commandManager.ExecuteCommand(cmd);
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

    public void Connect(string fromComponent, string fromPin, string toComponent, string toPin, double resistance = 0.0)
    {
        var cmd = new ConnectNodesCommand(_circuit, fromComponent, fromPin, toComponent, toPin)
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

    public void SetProperty(string componentName, string propertyName, double value)
    {
        var cmd = new UpdatePropertyCommand(_circuit, componentName, propertyName, value)
        {
            Origin = CommandOrigin.Python
        };
        _commandManager.ExecuteCommand(cmd);
    }

    public void SetCustomProperty(string componentName, string propertyName, object value)
    {
        var comp = _circuit.FindChild<ComponentNode>(componentName);
        if (comp != null)
        {
            comp.CustomProperties[propertyName] = value;
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
    public SimulationResultsData Simulate(string analysisType = "OperatingPoint")
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

        return _simulationManager.Run(_circuit, settings);
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
    public void AddBulb(string name, double x, double y, double resistance = 50.0, double ratedVoltage = 6.0)
    {
        AddComponent("bulb", name, x, y, resistance);
        if (ratedVoltage != 6.0) SetProperty(name, "RatedVoltage", ratedVoltage);
    }

    public void AddLED(string name, double x, double y, string color = "Red", double forwardVoltage = 2.0)
    {
        AddComponent("led", name, x, y, forwardVoltage);
        if (!string.Equals(color, "Red", StringComparison.OrdinalIgnoreCase))
            SetCustomProperty(name, "ColorName", color);
    }

    public void AddLogicInput(string name, double x, double y, bool initialState = false, double highVoltage = 5.0, double lowVoltage = 0.0)
    {
        AddComponent("logicinput", name, x, y, highVoltage);
        if (initialState) SetCustomProperty(name, "State", true);
        if (lowVoltage != 0.0) SetProperty(name, "LowVoltage", lowVoltage);
    }

    public void AddLogicOutput(string name, double x, double y, double threshold = 2.5)
    {
        AddComponent("logicoutput", name, x, y, threshold);
    }

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

    public void ToggleWaveformDrawer()
    {
        UI.Workspace.ToggleWaveform();
        OnWaveformDrawerToggleRequested?.Invoke();
    }

    public void ToggleView()
    {
        OnViewToggleRequested?.Invoke();
    }
}


