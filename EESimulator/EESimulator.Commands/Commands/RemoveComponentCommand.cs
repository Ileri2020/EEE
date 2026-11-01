using System;
using System.Collections.Generic;
using System.Linq;
using EESimulator.Core.Models;

namespace EESimulator.Commands.Commands;

/// <summary>
/// Command to remove a component and all its connected wires/connections from the circuit,
/// with full undo and redo capability.
/// </summary>
public class RemoveComponentCommand : ICommand
{
    public Guid Id { get; } = Guid.NewGuid();
    public string Description => $"Delete component {Component.Reference}";
    public CommandOrigin Origin { get; set; } = CommandOrigin.Schematic;

    public CircuitNode Circuit { get; }
    public ComponentNode Component { get; }
    private readonly List<ConnectionNode> _removedConnections = new();

    public RemoveComponentCommand(CircuitNode circuit, ComponentNode component)
    {
        Circuit = circuit ?? throw new ArgumentNullException(nameof(circuit));
        Component = component ?? throw new ArgumentNullException(nameof(component));
    }

    public bool Execute()
    {
        // Find and snapshot all connections attached to this component
        _removedConnections.Clear();
        foreach (var conn in Circuit.FindChildren<ConnectionNode>())
        {
            if (conn.FromComponentId == Component.Id || conn.ToComponentId == Component.Id)
            {
                _removedConnections.Add(conn);
            }
        }

        // Remove connections
        foreach (var conn in _removedConnections)
        {
            Circuit.RemoveChild(conn);
        }

        // Remove the component
        return Circuit.RemoveChild(Component);
    }

    public bool Undo()
    {
        // Restore component
        Circuit.AddChild(Component);

        // Restore connections
        foreach (var conn in _removedConnections)
        {
            Circuit.AddChild(conn);
        }

        return true;
    }

    public string ToPythonScript()
    {
        return $"circuit.remove_component(\"{Component.Name}\")";
    }
}
