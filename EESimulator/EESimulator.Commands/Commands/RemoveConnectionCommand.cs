using System;
using System.Text.Json;
using EESimulator.Core.Models;

namespace EESimulator.Commands.Commands;

/// <summary>
/// Removes a wire connection with undo/redo and script synchronization support.
/// </summary>
public sealed class RemoveConnectionCommand : ICommand
{
    public Guid Id { get; } = Guid.NewGuid();
    public string Description => $"Delete wire {Connection.NetName}";
    public CommandOrigin Origin { get; set; } = CommandOrigin.Schematic;

    public CircuitNode Circuit { get; }
    public ConnectionNode Connection { get; }

    public RemoveConnectionCommand(CircuitNode circuit, ConnectionNode connection)
    {
        Circuit = circuit ?? throw new ArgumentNullException(nameof(circuit));
        Connection = connection ?? throw new ArgumentNullException(nameof(connection));
    }

    public bool Execute() => Circuit.RemoveChild(Connection);

    public bool Undo()
    {
        Circuit.AddChild(Connection);
        return true;
    }

    public string ToPythonScript()
    {
        var fromComponent = Circuit.FindChildById<ComponentNode>(Connection.FromComponentId)
            ?? throw new InvalidOperationException("The wire's source component no longer exists.");
        var toComponent = Circuit.FindChildById<ComponentNode>(Connection.ToComponentId)
            ?? throw new InvalidOperationException("The wire's destination component no longer exists.");

        return $"circuit.remove_connection({JsonSerializer.Serialize(fromComponent.Name)}, " +
               $"{JsonSerializer.Serialize(Connection.FromPin)}, {JsonSerializer.Serialize(toComponent.Name)}, " +
               $"{JsonSerializer.Serialize(Connection.ToPin)})";
    }
}
