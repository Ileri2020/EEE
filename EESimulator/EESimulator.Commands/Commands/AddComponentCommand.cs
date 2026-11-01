using System;
using System.Globalization;
using EESimulator.Core.Models;

namespace EESimulator.Commands.Commands;

/// <summary>
/// Command to add an electrical component to the circuit hierarchy.
/// </summary>
public class AddComponentCommand : ICommand
{
    public Guid Id { get; } = Guid.NewGuid();
    public string Description => $"Add component {ComponentName} ({ComponentType})";
    public CommandOrigin Origin { get; set; } = CommandOrigin.Schematic;

    public CircuitNode Circuit { get; }
    public string ComponentType { get; }
    public string ComponentName { get; }
    public double X { get; }
    public double Y { get; }
    public double ParameterValue { get; }

    public ComponentNode? CreatedComponent { get; private set; }

    public AddComponentCommand(CircuitNode circuit, string componentType, string componentName, double x, double y, double parameterValue = 0)
    {
        Circuit = circuit ?? throw new ArgumentNullException(nameof(circuit));
        ComponentType = componentType;
        ComponentName = componentName;
        X = x;
        Y = y;
        ParameterValue = parameterValue;
    }

    public bool Execute()
    {
        if (CreatedComponent == null)
        {
            CreatedComponent = ComponentType.ToLowerInvariant() switch
            {
                "resistor" => new ResistorNode
                {
                    Name = ComponentName,
                    Reference = ComponentName,
                    X = X,
                    Y = Y,
                    Resistance = ParameterValue > 0 ? ParameterValue : 1000.0
                },
                "capacitor" => new CapacitorNode
                {
                    Name = ComponentName,
                    Reference = ComponentName,
                    X = X,
                    Y = Y,
                    Capacitance = ParameterValue > 0 ? ParameterValue : 1e-6
                },
                "inductor" => new InductorNode
                {
                    Name = ComponentName,
                    Reference = ComponentName,
                    X = X,
                    Y = Y,
                    Inductance = ParameterValue > 0 ? ParameterValue : 1e-3
                },
                "voltagesource" or "vsource" => new VoltageSourceNode
                {
                    Name = ComponentName,
                    Reference = ComponentName,
                    X = X,
                    Y = Y,
                    Voltage = ParameterValue != 0 ? ParameterValue : 5.0
                },
                "currentsource" or "isource" => new CurrentSourceNode
                {
                    Name = ComponentName,
                    Reference = ComponentName,
                    X = X,
                    Y = Y,
                    Current = ParameterValue != 0 ? ParameterValue : 0.001
                },
                "ground" or "gnd" => new GroundNode
                {
                    Name = ComponentName,
                    Reference = ComponentName,
                    X = X,
                    Y = Y
                },
                "mutualinductance" or "mutual" or "k" => new MutualInductanceNode
                {
                    Name = ComponentName,
                    Reference = ComponentName,
                    X = X,
                    Y = Y,
                    Coupling = ParameterValue > 0 ? Math.Clamp(ParameterValue, 0.0, 1.0) : 0.99
                },
                _ => EESimulator.Core.Components.Registry.ComponentRegistry.TryGetDefinition(ComponentType, out _)
                    ? EESimulator.Core.Components.Registry.ComponentRegistry.CreateInstance(ComponentType, ComponentName, X, Y)
                    : throw new ArgumentException($"Unknown component type: {ComponentType}")
            };
        }

        Circuit.AddChild(CreatedComponent);
        return true;
    }

    public bool Undo()
    {
        if (CreatedComponent != null)
        {
            return Circuit.RemoveChild(CreatedComponent);
        }
        return false;
    }

    public string ToPythonScript()
    {
        var invCulture = CultureInfo.InvariantCulture;
        var paramArg = ComponentType.ToLowerInvariant() switch
        {
            "resistor" => $", resistance={ParameterValue.ToString(invCulture)}",
            "capacitor" => $", capacitance={ParameterValue.ToString(invCulture)}",
            "inductor" => $", inductance={ParameterValue.ToString(invCulture)}",
            "voltagesource" or "vsource" => $", voltage={ParameterValue.ToString(invCulture)}",
            "currentsource" or "isource" => $", current={ParameterValue.ToString(invCulture)}",
            "mutualinductance" or "mutual" or "k" => $", coupling={ParameterValue.ToString(invCulture)}",
            _ => ""
        };

        var varName = ComponentName.ToLowerInvariant();
        return $"{varName} = circuit.add_component(\"{ComponentType}\", name=\"{ComponentName}\", position=({X.ToString(invCulture)}, {Y.ToString(invCulture)}){paramArg})";
    }
}
