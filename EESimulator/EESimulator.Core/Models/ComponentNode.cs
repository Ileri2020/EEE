using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace EESimulator.Core.Models;

/// <summary>
/// Represents an electrical component terminal or pin for netlist connections.
/// </summary>
public class ComponentPin
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public double RelativeX { get; set; }
    public double RelativeY { get; set; }

    public ComponentPin() { }

    public ComponentPin(string id, string name, double relX, double relY)
    {
        Id = id;
        Name = name;
        RelativeX = relX;
        RelativeY = relY;
    }
}

/// <summary>
/// Abstract base node for all schematic circuit components.
/// </summary>
public abstract class ComponentNode : RootNode
{
    public string Reference { get; set; } = string.Empty;

    [JsonIgnore]
    public virtual string Description
    {
        get => CustomProperties.TryGetValue("Description", out var description) && description != null
            ? description.ToString() ?? string.Empty
            : this is EESimulator.Core.Components.Base.ComponentBase componentBase
                ? componentBase.Description
                : string.Empty;
        set => CustomProperties["Description"] = value;
    }

    public double X { get; set; }
    public double Y { get; set; }

    public (double X, double Y) Position
    {
        get => (X, Y);
        set
        {
            X = value.X;
            Y = value.Y;
        }
    }

    public double Rotation { get; set; }

    public double ScaleX { get; set; } = 1.0;
    public double ScaleY { get; set; } = 1.0;

    public abstract string ComponentType { get; }

    public List<ComponentPin> Pins { get; private set; } = new();

    public Dictionary<string, double> ElectricalParameters { get; set; } = new();

    // Live probe and waveform monitoring states
    public bool ShowLiveProbe { get; set; } = false;
    public bool ShowWaveform { get; set; } = false;
    public double LiveVoltage { get; set; } = 0.0;
    public double LiveCurrent { get; set; } = 0.0;

    /// <summary>
    /// Maximum safe breaking current (A) before device breakdown / overload occurs.
    /// Default values are specialized per component category (e.g. 0.05A for logic gates, 0.03A for LEDs).
    /// </summary>
    public double BreakingCurrent
    {
        get => ElectricalParameters.TryGetValue("BreakingCurrent", out var val) ? val : GetDefaultBreakingCurrent();
        set
        {
            ElectricalParameters["BreakingCurrent"] = value;
            NotifyModified(nameof(BreakingCurrent));
        }
    }

    /// <summary>
    /// Indicates whether the component is currently exceeding its safe maximum breaking current.
    /// </summary>
    public bool IsBrokenDown => BreakingCurrent > 0 && Math.Abs(LiveCurrent) > BreakingCurrent;

    private double GetDefaultBreakingCurrent()
    {
        return ComponentType switch
        {
            "LogicInput" or "LogicOutput" or "LogicGate" or "AND" or "OR" or "NOT" or "NAND" or "NOR" or "XOR" or "XNOR" => 0.05, // 50mA logic breaking current
            "LED" => 0.03, // 30mA max LED current
            "Bulb" or "Lamp" => 1.5, // 1.5A bulb rating
            "Diode" => 1.0, // 1A standard diode
            "NPN" or "PNP" or "BJT" => 0.5, // 500mA
            "NMOS" or "PMOS" or "MOSFET" or "JFET" => 0.8,
            "OpAmp" => 0.04, // 40mA max opamp output
            "Resistor" => 0.5, // 0.5A
            "Wire" => 15.0, // 15A wire fusing current
            _ => 1.0
        };
    }

    public List<double> WaveformTime { get; private set; } = new();
    public List<double> WaveformVoltage { get; private set; } = new();
    public List<double> WaveformCurrent { get; private set; } = new();

    [JsonIgnore]
    public Dictionary<string, List<double>> WaveformNodeVoltages { get; private set; } = new(StringComparer.OrdinalIgnoreCase);

    [JsonIgnore]
    private List<SimulationEventTrigger>? _eventTriggers;

    /// <summary>User-configured measurement events. Persisted in StringParameters.</summary>
    [JsonIgnore]
    public List<SimulationEventTrigger> EventTriggers
    {
        get
        {
            if (_eventTriggers != null) return _eventTriggers;
            if (CustomProperties.TryGetValue("SimulationEventTriggers", out var raw) && raw is string json)
            {
                try { _eventTriggers = JsonSerializer.Deserialize<List<SimulationEventTrigger>>(json) ?? new List<SimulationEventTrigger>(); }
                catch (JsonException) { _eventTriggers = new List<SimulationEventTrigger>(); }
            }
            else _eventTriggers = new List<SimulationEventTrigger>();
            return _eventTriggers;
        }
        set
        {
            _eventTriggers = value ?? new List<SimulationEventTrigger>();
            PersistEventTriggers();
        }
    }

    public void PersistEventTriggers() =>
        CustomProperties["SimulationEventTriggers"] = JsonSerializer.Serialize(EventTriggers);

    /// <summary>Creates a detached copy with independent component collections.</summary>
    public virtual ComponentNode Clone()
    {
        var clone = (ComponentNode)MemberwiseClone();
        clone.Id = Guid.NewGuid();
        clone.ResetHierarchyForClone();
        clone.Pins = Pins.Select(pin => new ComponentPin(pin.Id, pin.Name, pin.RelativeX, pin.RelativeY)).ToList();
        clone.ElectricalParameters = new Dictionary<string, double>(ElectricalParameters);
        clone.CustomProperties = new Dictionary<string, object>(CustomProperties);
        clone.WaveformTime = new List<double>(WaveformTime);
        clone.WaveformVoltage = new List<double>(WaveformVoltage);
        clone.WaveformCurrent = new List<double>(WaveformCurrent);
        clone.WaveformNodeVoltages = WaveformNodeVoltages.ToDictionary(
            pair => pair.Key,
            pair => new List<double>(pair.Value),
            StringComparer.OrdinalIgnoreCase);
        var clonedTriggers = EventTriggers
            .Select(trigger => JsonSerializer.Deserialize<SimulationEventTrigger>(JsonSerializer.Serialize(trigger))!)
            .ToList();
        foreach (var trigger in clonedTriggers)
            trigger.Id = Guid.NewGuid().ToString("N");
        clone.EventTriggers = clonedTriggers;
        return clone;
    }

    public ComponentPin? GetPin(string pinNameOrId)
    {
        return Pins.Find(p => p.Id.Equals(pinNameOrId, StringComparison.OrdinalIgnoreCase) ||
                              p.Name.Equals(pinNameOrId, StringComparison.OrdinalIgnoreCase));
    }
}

public class ResistorNode : EESimulator.Core.Components.Electrical.ResistorComponent
{
    public ResistorNode() : base() { }
    public ResistorNode(string name, double resistance = 1000.0) : base(name, resistance) { }
}

public class CapacitorNode : EESimulator.Core.Components.Electrical.CapacitorComponent
{
    public CapacitorNode() : base() { }
    public CapacitorNode(string name, double capacitance = 1e-6) : base(name, capacitance) { }
}

public class InductorNode : EESimulator.Core.Components.Electrical.InductorComponent
{
    public InductorNode() : base() { }
    public InductorNode(string name, double inductance = 1e-3) : base(name, inductance) { }
}

public class VoltageSourceNode : EESimulator.Core.Components.Electrical.VoltageSourceComponent
{
    public VoltageSourceNode() : base() { }
    public VoltageSourceNode(string name, double voltage = 5.0) : base(name, voltage) { }
}

public class GroundNode : EESimulator.Core.Components.Electrical.GroundComponent
{
    public GroundNode() : base() { }
    public GroundNode(string name) : base(name) { }
}

public class MutualInductanceNode : EESimulator.Core.Components.Electrical.MutualInductanceComponent
{
    public MutualInductanceNode() : base() { }
    public MutualInductanceNode(string name, string ind1 = "L1", string ind2 = "L2", double coupling = 0.99)
        : base(name, ind1, ind2, coupling) { }
}

public class CurrentSourceNode : EESimulator.Core.Components.Electrical.CurrentSourceComponent
{
    public CurrentSourceNode() : base() { }
    public CurrentSourceNode(string name, double current = 0.001) : base(name, current) { }
}

public class BulbNode : EESimulator.Core.Components.Electrical.BulbComponent
{
    public BulbNode() : base() { }
    public BulbNode(string name, double resistance = 50.0) : base(name, resistance) { }
}

public class LogicInputNode : EESimulator.Core.Components.Electronics.LogicInputComponent
{
    public LogicInputNode() : base() { }
    public LogicInputNode(string name, bool initialState = false) : base(name, initialState) { }
}

public class LogicOutputNode : EESimulator.Core.Components.Electronics.LogicOutputComponent
{
    public LogicOutputNode() : base() { }
    public LogicOutputNode(string name, double threshold = 2.5) : base(name, threshold) { }
}


