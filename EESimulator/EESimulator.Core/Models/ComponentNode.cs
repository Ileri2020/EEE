using System;
using System.Collections.Generic;

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

    public double X { get; set; }
    public double Y { get; set; }

    public double Rotation { get; set; }

    public abstract string ComponentType { get; }

    public List<ComponentPin> Pins { get; } = new();

    public Dictionary<string, double> ElectricalParameters { get; set; } = new();

    // Live probe and waveform monitoring states
    public bool ShowLiveProbe { get; set; } = false;
    public bool ShowWaveform { get; set; } = false;
    public double LiveVoltage { get; set; } = 0.0;
    public double LiveCurrent { get; set; } = 0.0;

    public List<double> WaveformTime { get; } = new();
    public List<double> WaveformVoltage { get; } = new();
    public List<double> WaveformCurrent { get; } = new();

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

