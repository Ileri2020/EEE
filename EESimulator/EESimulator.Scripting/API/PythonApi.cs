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

    public void Connect(string fromComponent, string fromPin, string toComponent, string toPin)
    {
        var cmd = new ConnectNodesCommand(_circuit, fromComponent, fromPin, toComponent, toPin)
        {
            Origin = CommandOrigin.Python
        };
        _commandManager.ExecuteCommand(cmd);
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
}
